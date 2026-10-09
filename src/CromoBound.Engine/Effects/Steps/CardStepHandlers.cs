using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

internal sealed class DrawHandler : StepHandler<DrawStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, DrawStep step)
    {
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        var players = PlayerResolver.Resolve(game, task.Context, step.Player);
        if (amount <= 0 || players.Count == 0) return StepOutcome.DidNothing;
        foreach (var player in players) game.Draw(player, amount);
        task.Result = new EffectVar([], players, amount, true);
        return StepOutcome.Done;
    }
}

internal sealed class BurnHandler : StepHandler<BurnStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, BurnStep step)
    {
        var amount = ValueResolver.Resolve(game, task.Context, step.Amount);
        var players = PlayerResolver.Resolve(game, task.Context, step.Player);
        if (amount <= 0 || players.Count == 0) return StepOutcome.DidNothing;
        foreach (var player in players) game.Burn(player, amount);
        task.Result = new EffectVar([], players, amount, true);
        return StepOutcome.Done;
    }
}

/// <summary>Predict 1 (spec §8.1, Vision): the player looks at the top card of their Main Deck (their own event has the card,
/// the public copy only the count) and may recycle it. After a manual action it looks again only if the top card changed.</summary>
internal sealed class PredictHandler : StepHandler<PredictStep>
{
    public const string Question = "Recycle the top card of your Main Deck?";

    protected override StepOutcome Run(Game game, ResolveEffectTask task, PredictStep step)
    {
        var players = PlayerResolver.Resolve(game, task.Context, step.Player);
        if (players.Count != 1) return StepOutcome.DidNothing;
        var player = players[0];
        var deck = game.State.At(Place.MainDeck(player));
        if (deck.Count == 0) return StepOutcome.DidNothing;
        var top = deck[0];
        if (task.Answer is bool recycle && task.Progress is ObjectId seen && seen == top)
        {
            if (recycle) game.Recycle(top);
            task.Result = new EffectVar([], [player], 1, true);
            return StepOutcome.Done;
        }
        if (task.Progress is not ObjectId looked || looked != top)
        {
            task.Progress = top;
            game.Emit(new Predicted(player, 1, [game.State[top].CardId]) { VisibleTo = player });
            game.Emit(new Predicted(player, 1, null));
        }
        game.Ask(new OptionalDecision(player, task.Context.SourceCardId, Question), (_, action) =>
        {
            if (action is not ChooseOptional choice) return Game.Reject(RejectionCode.UnexpectedAction, "Answer yes or no.");
            task.Answer = choice.Yes;
            return null;
        });
        return StepOutcome.Asked;
    }
}
