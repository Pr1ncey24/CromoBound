using CromoBound.Engine.Rules;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns conditions into true or false (spec §4.5): all, any, not, empowered (the source is Empowered) and legion (the
/// controller played a card this turn; read while a card is being played, so its own play isn't counted yet).
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
        throw new InvalidOperationException("Only all, any, not, empowered and legion conditions are supported so far.");
    }
}
