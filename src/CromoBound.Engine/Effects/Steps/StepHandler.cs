using CromoBound.Engine.Rules;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

/// <summary>Done: the step happened. DidNothing: there was nothing to act on. Asked: it raised a decision and runs again after the answer.</summary>
internal enum StepOutcome { Done, DidNothing, Asked }

/// <summary>Runs one kind of step (spec §4.4). Called again with the same task after it asked.</summary>
internal interface IStepHandler
{
    StepOutcome Run(Game game, ResolveEffectTask task, Step step);
}

internal abstract class StepHandler<TStep> : IStepHandler where TStep : Step
{
    public StepOutcome Run(Game game, ResolveEffectTask task, Step step) => Run(game, task, (TStep)step);

    protected abstract StepOutcome Run(Game game, ResolveEffectTask task, TStep step);
}
