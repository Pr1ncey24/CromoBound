using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

/// <summary>"Give a unit +N this turn": a might modifier on each unit aimed at (spec §3.2). It records its units and amount even
/// when the amount is 0, so a later step can read them (Rampage).</summary>
internal sealed class ModifyMightHandler : StepHandler<ModifyMightStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, ModifyMightStep step)
    {
        if (ResolutionChoice.Targets(game, task, step.Target) is not { } aimed) return StepOutcome.Asked;
        List<ObjectId> units = [.. aimed.Where(id => game.State[id].Place.IsLocation && game.IsUnit(game.State[id]))];
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        task.Result = new EffectVar(units, [], amount, false);
        if (units.Count == 0 || amount == 0) return StepOutcome.DidNothing;
        foreach (var unit in units) game.ModifyMight(unit, amount, step.Duration ?? Duration.ThisTurn);
        return StepOutcome.Done;
    }
}

/// <summary>"Move a unit at a battlefield to its base" (Amateur Recital): each aimed unit at a battlefield goes to its controller's
/// base. Not a standard move: nothing is exhausted and no Contested applies (Plan O deviation 2).</summary>
internal sealed class MoveHandler : StepHandler<MoveStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, MoveStep step)
    {
        if (ResolutionChoice.Targets(game, task, step.Target) is not { } aimed) return StepOutcome.Asked;
        List<ObjectId> units = [.. aimed.Where(id => game.State[id].Place.Kind == PlaceKind.Battlefield && game.IsUnit(game.State[id]))];
        task.Result = new EffectVar(units, [], null, false);
        if (units.Count == 0) return StepOutcome.DidNothing;
        foreach (var unit in units) game.MoveCard(unit, Place.Base(game.State[unit].Controller));
        return StepOutcome.Done;
    }
}
