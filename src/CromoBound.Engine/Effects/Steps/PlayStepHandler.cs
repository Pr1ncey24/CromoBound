using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using EffectPlayStep = CromoBound.Models.Effects.PlayStep;

namespace CromoBound.Engine.Effects.Steps;

/// <summary>Plays a stored card (spec §5.5) through 2a's <see cref="PlayCardTask"/>, from wherever it is, for its cost or ignoring it.
/// The step asks nothing itself: it starts the play, waits until the play task is done, then runs again. A cancelled play did nothing.
/// The play task starts as started, so cleanups and triggers wait for the whole resolution (CR 321).</summary>
internal sealed class PlayHandler : StepHandler<EffectPlayStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, EffectPlayStep step)
    {
        if (task.Answer is PlayCardTask running)
        {
            if (!running.Finished) return StepOutcome.DidNothing;
            task.Result = new EffectVar([running.Played!.Value], [], null, true);
            return StepOutcome.Done;
        }
        List<ObjectId> cards =
        [
            .. ObjectResolver.Resolve(game, task.Context, step.Card)
                .Where(id => !game.State[id].Place.IsLocation && game.State[id].Place.Kind != PlaceKind.Chain),
        ];
        if (cards.Count == 0) return StepOutcome.DidNothing;
        var play = new PlayCardTask(task.Context.Controller, cards[0]) { IgnoreCost = step.Cost == PlayCostMode.IgnoreAll, Started = true };
        game.Push(play);
        task.Answer = play;
        return StepOutcome.Asked;
    }
}
