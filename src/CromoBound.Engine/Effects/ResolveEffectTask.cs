using CromoBound.Engine.Effects.Steps;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects;

/// <summary>Runs an ability's steps in order with a step counter (spec §4.3). A step that asks pauses the task; after the answer
/// (or after a manual action rebuilt the decision) the same step runs again. <paramref name="onDone"/> runs once, after the last step.
/// The task stops when the game ends, and, when <paramref name="item"/> is given, as soon as that chain item leaves the chain
/// (countered while a step waited for an answer).</summary>
internal sealed class ResolveEffectTask(EffectContext context, IReadOnlyList<Step> steps, Action<Game> onDone, ChainItem? item = null) : GameTask
{
    public EffectContext Context { get; } = context;
    public IReadOnlyList<Step> Steps { get; } = steps;
    public ChainItem? Item { get; } = item;
    public int Index { get; set; }

    /// <summary>The current step's result, saved under its "store" name when the step finishes.</summary>
    public EffectVar? Result { get; set; }

    /// <summary>The answer to the current step's decision, set by the decision's handler; the step reads it when it runs again.</summary>
    public object? Answer { get; set; }

    /// <summary>What the current step did before asking, kept across reruns (Predict: the card it looked at).</summary>
    public object? Progress { get; set; }

    public override bool Run(Game game)
    {
        while (Index < Steps.Count)
        {
            if (Stopped(game)) return true;
            var step = Steps[Index];
            var outcome = StepRegistry.For(step).Run(game, this, step);
            if (outcome == StepOutcome.Asked) return false;
            if (step.Store is { } name) Context.Vars[name] = (Result ?? EffectVar.Empty) with { Happened = outcome == StepOutcome.Done };
            Result = null;
            Answer = null;
            Progress = null;
            Index++;
        }
        if (!Stopped(game)) onDone(game);
        return true;
    }

    private bool Stopped(Game game) => game.Outcome is not null || (Item is not null && !game.State.Chain.Contains(Item));
}
