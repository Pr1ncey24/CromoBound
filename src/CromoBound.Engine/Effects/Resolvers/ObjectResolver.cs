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

    /// <summary>Whether a card matches the filter's relation, type, token and other fields (the ones <see cref="EffectsSupport"/> lets through).</summary>
    public static bool FilterMatches(Game game, EffectContext context, Filter? filter, CardInstance instance)
    {
        if (filter is null) return true;
        if (filter.Relation == Relation.Friendly && instance.Controller != context.Controller) return false;
        if (filter.Relation == Relation.Enemy && instance.Controller == context.Controller) return false;
        if (filter.Type is { } wanted && game.CardOf(instance).Type != wanted) return false;
        if (filter.Token is { } token && instance.IsToken != token) return false;
        if (filter.Other == true && context.Source == instance.Id) return false;
        return true;
    }
}
