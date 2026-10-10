using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>Values effects change while they apply (spec §4.7). Evaluated live: nothing is stored, so replay stays trivial.</summary>
internal static class Modifiers
{
    /// <summary>Printed Might, +1 while buffed, manual and effect Might modifiers, ModifyMight passives (its own and its gear's),
    /// Assault X while attacking and Shield X while defending (CR 807, 814).</summary>
    public static int MightOf(Game game, CardInstance unit)
    {
        var combat = unit.Role switch
        {
            CombatRole.Attacker => KeywordValue(game, unit, MechanicalKeyword.Assault),
            CombatRole.Defender => KeywordValue(game, unit, MechanicalKeyword.Shield),
            _ => 0,
        };
        return (game.CardOf(unit).Might ?? 0) + (unit.Buffed ? 1 : 0) + unit.Modifiers.Sum(m => m.Amount) + combat + PassiveMight(game, unit);
    }

    /// <summary>The sum of a numbered keyword's values on the card, printed and granted: a missing value is 1, and several
    /// instances stack.</summary>
    public static int KeywordValue(Game game, CardInstance instance, MechanicalKeyword keyword) =>
        game.Effects.For(instance.CardId).KeywordEntries.Concat(GrantedKeywords(game, instance))
            .Where(k => k.Keyword == keyword).Sum(k => k.Value ?? 1);

    /// <summary>The keyword entries the card's passives grant it now.</summary>
    public static IEnumerable<KeywordEntry> GrantedKeywords(Game game, CardInstance instance) =>
        ActiveModifiers(game, instance).Select(m => m.Modifier).OfType<GrantKeywordModifier>().Select(g => g.Keyword);

    private static int PassiveMight(Game game, CardInstance unit) =>
        ActiveModifiers(game, unit).Where(m => m.Modifier is ModifyMightModifier)
            .Sum(m => ValueResolver.Resolve(game, m.Context, ((ModifyMightModifier)m.Modifier).Amount));

    /// <summary>The modifiers that apply to the holder now:
    /// - its own passives with appliesTo Self;
    /// - the passives of gear attached to it with appliesTo Host.
    /// In both cases the source must be on the board and the passive's condition and while must hold. While a holder's are
    /// evaluated, a nested might or keyword read of it sees only printed values, so a condition that reads its might can't loop.</summary>
    public static IEnumerable<(Modifier Modifier, EffectContext Context)> ActiveModifiers(Game game, CardInstance holder)
    {
        if (!holder.Place.IsLocation || !game.EvaluatingPassives.Add(holder.Id)) return [];
        try
        {
            List<(Modifier, EffectContext)> found = [.. Applying(game, holder, holder, RefKind.Self)];
            foreach (var gear in game.State.Objects.Where(o => o.AttachedTo == holder.Id && o.Place.IsLocation))
                found.AddRange(Applying(game, gear, holder, RefKind.Host));
            return found;
        }
        finally
        {
            game.EvaluatingPassives.Remove(holder.Id);
        }
    }

    private static IEnumerable<(Modifier, EffectContext)> Applying(Game game, CardInstance source, CardInstance holder, RefKind appliesTo)
    {
        var context = new EffectContext { Controller = source.Controller, Source = source.Id, SourceCardId = source.CardId };
        return game.Effects.For(source.CardId).Abilities.OfType<PassiveAbility>()
            .Where(p => (p.Condition is null || ConditionResolver.Holds(game, context, p.Condition))
                && (p.While is null || ConditionResolver.Holds(game, context, p.While)))
            .SelectMany(p => p.Modifiers)
            .Where(m => m.AppliesTo?.Ref == appliesTo)
            .Select(m => (m, context));
    }

    /// <summary>A play's total cost (spec §4.7, rule 356):
    /// - the base cost (none from Hidden or when an effect ignores it), plus Accelerate;
    /// - then one [A] per Deflect on each target an opponent of the player controls, counted per choice (CR 809);
    /// - then the card's own energy reductions (Legion), energy never below 0.</summary>
    public static TotalCost CostOf(Game game, PlayCardTask task)
    {
        var item = task.Item!;
        var card = game.State[item.Card!.Value];
        var cost = Payment.CostOf(game.CardOf(card), task.FromHidden || task.IgnoreCost, item.Accelerate);
        List<PowerSymbol> power = [.. cost.Power];
        IEnumerable<ObjectId> targets = item.Effect is { } effect ? effect.Targets.SelectMany(t => t) : [];
        foreach (var target in targets)
            if (game.State.Exists(target) && game.State[target].Controller != task.Player)
                power.AddRange(Enumerable.Repeat(PowerSymbol.Any, KeywordValue(game, game.State[target], MechanicalKeyword.Deflect)));
        var context = new EffectContext { Controller = task.Player, Source = card.Id, SourceCardId = card.CardId };
        var reduction = OwnPassives(game, card, context).OfType<CostReductionModifier>().Sum(m => ValueResolver.Resolve(game, context, m.Energy!));
        return new TotalCost(Math.Max(0, cost.Energy - reduction), power);
    }

    /// <summary>Whether one of the card's passive abilities gives the card the permission now (spec §4.7).</summary>
    public static bool Permits(Game game, CardInstance card, PlayerId controller, Permission permission) =>
        OwnPassives(game, card, new EffectContext { Controller = controller, Source = card.Id, SourceCardId = card.CardId })
            .OfType<PermissionModifier>()
            .Any(p => p.Permission == permission);

    /// <summary>The modifiers of the card's passive abilities that apply to the card itself and whose condition holds now.</summary>
    private static IEnumerable<Modifier> OwnPassives(Game game, CardInstance card, EffectContext context) =>
        game.Effects.For(card.CardId).Abilities.OfType<PassiveAbility>()
            .Where(p => (p.Condition is null || ConditionResolver.Holds(game, context, p.Condition))
                && (p.While is null || ConditionResolver.Holds(game, context, p.While)))
            .SelectMany(p => p.Modifiers)
            .Where(m => m.AppliesTo?.Ref == RefKind.Self);
}
