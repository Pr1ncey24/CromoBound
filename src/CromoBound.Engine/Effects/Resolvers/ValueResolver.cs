using CromoBound.Engine.Rules;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns values into numbers. Plan D runs literals; the support check keeps other forms out of the cards it runs.</summary>
internal static class ValueResolver
{
    public static int Resolve(Game game, EffectContext context, Value value) =>
        value.Literal ?? throw new InvalidOperationException("Only literal values are supported so far.");
}
