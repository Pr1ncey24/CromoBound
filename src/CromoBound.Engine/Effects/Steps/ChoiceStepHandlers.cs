using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Steps;

/// <summary>The controller chooses a player the filter allows (Opponent, Self, or anyone when there is no filter), offered in
/// turn order. A single option is forced (spec §6.2); every choice is announced with <see cref="PlayerChosen"/>.</summary>
internal sealed class ChoosePlayerHandler : StepHandler<ChoosePlayerStep>
{
    private static readonly PlayerRef Everyone = new() { Kind = PlayerKind.EachPlayer };

    protected override StepOutcome Run(Game game, ResolveEffectTask task, ChoosePlayerStep step)
    {
        var chooser = task.Context.Controller;
        List<PlayerId> options = [.. PlayerResolver.Resolve(game, task.Context, Everyone).Where(p => Allowed(step.Filter, chooser, p))];
        if (options.Count == 0) return StepOutcome.DidNothing;
        if (task.Answer is PlayerId answered) return Chosen(task, answered);
        if (options.Count == 1)
        {
            game.Emit(new ChoiceMade(chooser, "Player", []));
            game.Emit(new PlayerChosen(chooser, options[0]));
            return Chosen(task, options[0]);
        }
        game.Ask(new ChoosePlayerDecision(chooser, task.Context.SourceCardId, options), (_, action) =>
        {
            if (action is not ChoosePlayer choice || !options.Contains(choice.Player))
                return Game.Reject(RejectionCode.UnexpectedAction, "Choose one of the offered players.");
            game.Emit(new PlayerChosen(chooser, choice.Player));
            task.Answer = choice.Player;
            return null;
        });
        return StepOutcome.Asked;
    }

    private static StepOutcome Chosen(ResolveEffectTask task, PlayerId player)
    {
        task.Result = new EffectVar([], [player], null, true);
        return StepOutcome.Done;
    }

    private static bool Allowed(Filter? filter, PlayerId chooser, PlayerId player) => filter?.Relation switch
    {
        Relation.Opponent => player != chooser,
        Relation.Self => player == chooser,
        _ => true,
    };
}

/// <summary>The controller chooses cards from a zone without targeting (spec §6.1): as many as the count, or all there are if fewer.
/// When the options are exactly that many the choice is forced.</summary>
internal sealed class ChooseCardHandler : StepHandler<ChooseCardStep>
{
    protected override StepOutcome Run(Game game, ResolveEffectTask task, ChooseCardStep step)
    {
        var context = task.Context;
        List<ObjectId> options =
        [
            .. ZoneResolver.Resolve(game, context, step.From)
                .SelectMany(place => game.State.At(place))
                .Where(id => ObjectResolver.FilterMatches(game, context, step.Filter, game.State[id])),
        ];
        var count = Math.Min(ValueResolver.Resolve(game, context, step.Count), options.Count);
        if (count <= 0) return StepOutcome.DidNothing;
        if (task.Answer is IReadOnlyList<ObjectId> answered) return Chosen(task, answered);
        if (options.Count == count)
        {
            game.Emit(new ChoiceMade(context.Controller, "Cards", options));
            return Chosen(task, options);
        }
        game.Ask(new ChooseCardsDecision(context.Controller, context.SourceCardId, options, count, count), (_, action) =>
        {
            if (action is not ChooseCards choose) return Game.Reject(RejectionCode.UnexpectedAction, "Choose the cards.");
            if (Game.CheckPick(choose.Cards, options, count, count, "cards") is { } rejection) return rejection;
            List<ObjectId> picked = [.. choose.Cards];
            task.Answer = picked;
            return null;
        });
        return StepOutcome.Asked;
    }

    private static StepOutcome Chosen(ResolveEffectTask task, IReadOnlyList<ObjectId> cards)
    {
        task.Result = new EffectVar([.. cards], [], cards.Count, true);
        return StepOutcome.Done;
    }
}

/// <summary>"You may do this:" (spec §5.4). The controller answers yes or no; on yes the block becomes a new chain item with the
/// same context, so players get priority on it before it resolves.</summary>
internal sealed class OptionalHandler : StepHandler<OptionalStep>
{
    public const string Question = "Do the rest of the ability?";

    protected override StepOutcome Run(Game game, ResolveEffectTask task, OptionalStep step)
    {
        var context = task.Context;
        if (task.Answer is bool yes)
        {
            if (!yes) return StepOutcome.DidNothing;
            var parent = task.Item;
            game.AddAbilityItem(context.Controller, context.Source, context.SourceCardId, parent?.AbilityKind ?? AbilityKind.Triggered,
                parent?.Text, step.Steps, context);
            return StepOutcome.Done;
        }
        game.Ask(new OptionalDecision(context.Controller, context.SourceCardId, Question), (_, action) =>
        {
            if (action is not ChooseOptional choice) return Game.Reject(RejectionCode.UnexpectedAction, "Answer yes or no.");
            task.Answer = choice.Yes;
            return null;
        });
        return StepOutcome.Asked;
    }
}
