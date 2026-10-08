using CromoBound.Engine.Effects.Steps;
using CromoBound.Engine.Rules;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>Runs an ability's steps in order with a step counter (spec §4.3). A step that asks pauses the task; after the answer
/// (or after a manual action rebuilt the decision) the same step runs again. <paramref name="onDone"/> runs once, after the last step.</summary>
internal sealed class ResolveEffectTask(EffectContext context, IReadOnlyList<Step> steps, Action<Game> onDone) : GameTask
{
    public EffectContext Context { get; } = context;
    public IReadOnlyList<Step> Steps { get; } = steps;
    public int Index { get; set; }

    /// <summary>The current step's result, saved under its "store" name when the step finishes.</summary>
    public EffectVar? Result { get; set; }

    public override bool Run(Game game)
    {
        while (Index < Steps.Count)
        {
            if (game.Outcome is not null) return true;
            var step = Steps[Index];
            var outcome = StepRegistry.For(step).Run(game, this, step);
            if (outcome == StepOutcome.Asked) return false;
            if (step.Store is { } name) Context.Vars[name] = (Result ?? EffectVar.Empty) with { Happened = outcome == StepOutcome.Done };
            Result = null;
            Index++;
        }
        if (game.Outcome is null) onDone(game);
        return true;
    }
}
