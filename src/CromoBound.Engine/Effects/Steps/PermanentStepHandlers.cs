using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

internal sealed class DealHandler : StepHandler<DealStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, DealStep step)
    {
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        List<ObjectId> units =
        [
            .. ObjectResolver.Resolve(game, task.Context, step.Target)
                .Where(id => game.State[id].Place.IsLocation && game.IsUnit(game.State[id])),
        ];
        if (amount <= 0 || units.Count == 0) return StepOutcome.DidNothing;
        foreach (var unit in units) game.DealDamage(unit, amount);
        task.Result = new EffectVar(units, [], amount, true);
        return StepOutcome.Done;
    }
}

internal sealed class KillHandler : StepHandler<KillStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, KillStep step)
    {
        List<ObjectId> targets = [.. ObjectResolver.Resolve(game, task.Context, step.Target).Where(id => game.State[id].Place.IsLocation)];
        if (targets.Count == 0) return StepOutcome.DidNothing;
        foreach (var target in targets) game.Kill(target);
        task.Result = new EffectVar(targets, [], null, true);
        return StepOutcome.Done;
    }
}

/// <summary>Empowers the board objects that aren't Empowered yet (Empower, spec §8.1).</summary>
internal sealed class EmpowerHandler : StepHandler<EmpowerStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, EmpowerStep step)
    {
        List<ObjectId> targets =
        [
            .. ObjectResolver.Resolve(game, task.Context, step.Target)
                .Where(id => game.State[id].Place.IsLocation && !game.State[id].Empowered),
        ];
        if (targets.Count == 0) return StepOutcome.DidNothing;
        foreach (var target in targets) game.SetStatus(target, StatusKind.Empowered, true);
        task.Result = new EffectVar(targets, [], null, true);
        return StepOutcome.Done;
    }
}
