using CromoBound.Engine.Rules;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns conditions into true or false (spec §4.5). Plan E evaluates the forms keyword abilities use: all, any, not and
/// empowered (the source is Empowered). <see cref="EffectsSupport"/> keeps every other condition out of the files it runs.</summary>
internal static class ConditionResolver
{
    public static bool Holds(Game game, EffectContext context, Condition condition)
    {
        if (condition.All is { } all) return all.All(c => Holds(game, context, c));
        if (condition.Any is { } any) return any.Any(c => Holds(game, context, c));
        if (condition.Not is { } not) return !Holds(game, context, not);
        if (condition.Empowered is { } empowered)
            return context.Source is { } source && game.State.Exists(source) && game.State[source].Empowered == empowered;
        throw new InvalidOperationException("Only all, any, not and empowered conditions are supported so far.");
    }
}
