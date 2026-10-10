using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
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
        if (ResolutionChoice.Targets(game, task, step.Target) is not { } aimed) return StepOutcome.Asked;
        List<ObjectId> units = [.. aimed.Where(id => game.State[id].Place.IsLocation && game.IsUnit(game.State[id]))];
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        task.Result = new EffectVar(units, [], amount, false);
        if (amount <= 0 || units.Count == 0) return StepOutcome.DidNothing;
        foreach (var unit in units) game.DealDamage(unit, amount);
        return StepOutcome.Done;
    }
}

internal sealed class KillHandler : StepHandler<KillStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, KillStep step)
    {
        if (ResolutionChoice.Targets(game, task, step.Target) is not { } aimed) return StepOutcome.Asked;
        List<ObjectId> targets = [.. aimed.Where(id => game.State[id].Place.IsLocation)];
        task.Result = new EffectVar(targets, [], null, false);
        if (targets.Count == 0) return StepOutcome.DidNothing;
        foreach (var target in targets) game.Kill(target);
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

/// <summary>Equip (CR 818, spec §8.1): attaches the gear (the step's target, Self) to a unit chosen on resolution among the "to"
/// selector's candidates. One candidate is a forced choice.</summary>
internal sealed class AttachHandler : StepHandler<AttachStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, AttachStep step)
    {
        var context = task.Context;
        List<ObjectId> gear = [.. ObjectResolver.Resolve(game, context, step.Target).Where(id => game.State[id].Place.IsLocation)];
        var hosts = ObjectResolver.Candidates(game, context, step.To);
        if (gear.Count == 0 || hosts.Count == 0) return StepOutcome.DidNothing;
        if (task.Answer is IReadOnlyList<ObjectId> { Count: 1 } chosen) return Attached(game, task, gear[0], chosen[0]);
        if (hosts.Count == 1)
        {
            game.Emit(new ChoiceMade(context.Controller, "Cards", hosts));
            return Attached(game, task, gear[0], hosts[0]);
        }
        game.Ask(new ChooseCardsDecision(context.Controller, context.SourceCardId, hosts, 1, 1), (_, action) =>
        {
            if (action is not ChooseCards choose) return Game.Reject(RejectionCode.UnexpectedAction, "Choose the unit to attach to.");
            if (Game.CheckPick(choose.Cards, hosts, 1, 1, "units") is { } rejection) return rejection;
            List<ObjectId> picked = [.. choose.Cards];
            task.Answer = picked;
            return null;
        });
        return StepOutcome.Asked;
    }

    private static StepOutcome Attached(Game game, ResolveEffectTask task, ObjectId gear, ObjectId unit)
    {
        game.Attach(gear, unit);
        task.Result = new EffectVar([gear], [], null, true);
        return StepOutcome.Done;
    }
}
