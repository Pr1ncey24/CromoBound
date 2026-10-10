using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns values into numbers: literals, variables (their number, 0 when unset), an object's property (the first object;
/// 0 when there is none), sums and products. <see cref="EffectsSupport"/> keeps other forms out of the cards it runs.</summary>
internal static class ValueResolver
{
    public static int Resolve(Game game, EffectContext context, Value value)
    {
        if (value.Literal is { } literal) return literal;
        if (value.Var is { } name) return context.Vars.TryGetValue(name, out var stored) ? stored.Number ?? 0 : 0;
        if (value.Prop is { } prop)
            return ObjectResolver.Resolve(game, context, value.Of!).Select(id => Property(game, game.State[id], prop)).FirstOrDefault();
        if (value.Sum is { } sum) return sum.Sum(v => Resolve(game, context, v));
        if (value.Mul is { } mul) return mul.Aggregate(1, (total, v) => total * Resolve(game, context, v));
        throw new InvalidOperationException("Only literal, var, prop, sum and mul values are supported so far.");
    }

    private static int Property(Game game, CardInstance instance, ValueProperty prop) => prop switch
    {
        ValueProperty.Might => game.MightOf(instance.Id),
        ValueProperty.EnergyCost => game.CardOf(instance).Cost?.Energy ?? 0,
        ValueProperty.Damage => instance.Damage,
        ValueProperty.EmpowerCount => instance.EmpowerCount,
        _ => throw new InvalidOperationException($"The {prop} property isn't supported so far."),
    };
}
