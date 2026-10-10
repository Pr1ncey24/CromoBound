using CromoBound.Engine.Rules;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns conditions into true or false (spec §4.5): all, any, not, empowered (the source is Empowered), legion (the
/// controller played a card this turn; read while a card is being played, so its own play isn't counted yet), exists (a reference
/// finds an object), compare, paid (an additional cost recorded as paid) and turnOf.
/// <see cref="EffectsSupport"/> keeps every other condition out of the files it runs.</summary>
internal static class ConditionResolver
{
    public static bool Holds(Game game, EffectContext context, Condition condition)
    {
        if (condition.All is { } all) return all.All(c => Holds(game, context, c));
        if (condition.Any is { } any) return any.Any(c => Holds(game, context, c));
        if (condition.Not is { } not) return !Holds(game, context, not);
        if (condition.Empowered is { } empowered)
            return context.Source is { } source && game.State.Exists(source) && game.State[source].Empowered == empowered;
        if (condition.Legion is { } legion) return (game.State.Turn.PlayedThisTurn(context.Controller) > 0) == legion;
        if (condition.Exists is { } exists) return ObjectResolver.Resolve(game, context, exists).Count > 0;
        if (condition.Compare is { } compare)
            return Compare(ValueResolver.Resolve(game, context, compare.Left), compare.Op, ValueResolver.Resolve(game, context, compare.Right));
        if (condition.Paid is { } paid) return context.Vars.TryGetValue(paid, out var cost) && cost.Happened;
        if (condition.TurnOf is { } player) return PlayerResolver.Resolve(game, context, player).Contains(game.State.Turn.TurnPlayer);
        throw new InvalidOperationException("Only all, any, not, empowered, legion, exists, compare, paid and turnOf conditions are supported so far.");
    }

    private static bool Compare(int left, CompareOp op, int right) => op switch
    {
        CompareOp.Eq => left == right,
        CompareOp.Lte => left <= right,
        CompareOp.Gte => left >= right,
        CompareOp.Lt => left < right,
        _ => left > right,
    };
}
