using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>Values effects change while they apply (spec §4.7). Evaluated live: nothing is stored, so replay stays trivial.</summary>
internal static class Modifiers
{
    /// <summary>Printed Might, +1 while buffed, manual and effect Might modifiers, Assault X while attacking and Shield X while
    /// defending (CR 807, 814).</summary>
    public static int MightOf(Game game, CardInstance unit)
    {
        var combat = unit.Role switch
        {
            CombatRole.Attacker => KeywordValue(game, unit, MechanicalKeyword.Assault),
            CombatRole.Defender => KeywordValue(game, unit, MechanicalKeyword.Shield),
            _ => 0,
        };
        return (game.CardOf(unit).Might ?? 0) + (unit.Buffed ? 1 : 0) + unit.Modifiers.Sum(m => m.Amount) + combat;
    }

    /// <summary>The sum of a numbered keyword's values on the card: a missing value is 1, and several instances stack.</summary>
    public static int KeywordValue(Game game, CardInstance instance, MechanicalKeyword keyword) =>
        game.Effects.For(instance.CardId).KeywordEntries.Where(k => k.Keyword == keyword).Sum(k => k.Value ?? 1);

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
            .Where(p => p.Condition is null || ConditionResolver.Holds(game, context, p.Condition))
            .SelectMany(p => p.Modifiers)
            .Where(m => m.AppliesTo?.Ref == RefKind.Self);
}
