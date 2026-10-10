using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns object references into objects (spec §4.5). Pure: reads the game, changes nothing.</summary>
internal static class ObjectResolver
{
    /// <summary>The board objects a selector can pick now, in id order.</summary>
    public static List<ObjectId> Candidates(Game game, EffectContext context, ObjectRef selector) =>
    [
        .. game.State.Objects
            .Where(o => o.Place.IsLocation && Matches(game, context, selector.Select!.Value, selector.Filter, o))
            .Select(o => o.Id),
    ];

    /// <summary>The objects a reference means now: Self (while it exists), a stored variable, a target slot's chosen targets
    /// (dropping those no longer legal), or a selector's candidates.</summary>
    public static List<ObjectId> Resolve(Game game, EffectContext context, ObjectRef reference)
    {
        if (reference.Ref == RefKind.Self)
            return context.Source is { } source && game.State.Exists(source) ? [source] : [];
        if (reference.Ref == RefKind.Host)
            return context.Source is { } gear && game.State.Exists(gear) && game.State[gear].AttachedTo is { } host && game.State.Exists(host)
                ? [host] : [];
        if (reference.Var is { } name)
            return context.Vars.TryGetValue(name, out var stored) ? [.. stored.Objects.Where(game.State.Exists)] : [];
        var slot = TargetSlots.IndexOf(context.Slots, reference);
        if (slot < 0) return Candidates(game, context, reference);
        if (slot >= context.Targets.Count) return [];
        var legal = Candidates(game, context, reference);
        return [.. context.Targets[slot].Where(legal.Contains)];
    }

    private static bool Matches(Game game, EffectContext context, SelectKind kind, Filter? filter, CardInstance instance)
    {
        var type = game.CardOf(instance).Type;
        var kindMatches = kind switch
        {
            SelectKind.Unit => type == CardType.Unit,
            SelectKind.Gear => type == CardType.Gear,
            SelectKind.Permanent => type is CardType.Unit or CardType.Gear,
            _ => false,
        };
        return kindMatches && FilterMatches(game, context, filter, instance);
    }

    /// <summary>Whether a card matches the filter's relation, type, token, other, mighty, location and not fields (the ones
    /// <see cref="EffectsSupport"/> lets through).</summary>
    public static bool FilterMatches(Game game, EffectContext context, Filter? filter, CardInstance instance)
    {
        if (filter is null) return true;
        if (filter.Relation == Relation.Friendly && instance.Controller != context.Controller) return false;
        if (filter.Relation == Relation.Enemy && instance.Controller == context.Controller) return false;
        if (filter.Type is { } wanted && game.CardOf(instance).Type != wanted) return false;
        if (filter.Token is { } token && instance.IsToken != token) return false;
        if (filter.Other == true && context.Source == instance.Id) return false;
        if (filter.Mighty is { } mighty && IsMighty(game, instance) != mighty) return false;
        if (filter.Location is { } location && !AtLocation(game, context, location, instance)) return false;
        if (filter.Not is { } not && FilterMatches(game, context, not, instance)) return false;
        return true;
    }

    /// <summary>A unit on the board with 5 or more might (CR 706-711).</summary>
    public static bool IsMighty(Game game, CardInstance instance) =>
        instance.Place.IsLocation && game.IsUnit(instance) && game.MightOf(instance.Id) >= 5;

    /// <summary>Here: the battlefield of the source (a battlefield card, or a permanent standing there). A battlefield selector:
    /// any battlefield.</summary>
    private static bool AtLocation(Game game, EffectContext context, ObjectRef location, CardInstance instance)
    {
        if (location.Select == SelectKind.Battlefield) return instance.Place.Kind == PlaceKind.Battlefield;
        if (location.Ref != RefKind.Here || context.Source is not { } source || !game.State.Exists(source)) return false;
        var at = game.State[source].Place;
        return at.Index is { } index && at.Kind is PlaceKind.BattlefieldCard or PlaceKind.Battlefield && instance.Place == Place.Battlefield(index);
    }
}
