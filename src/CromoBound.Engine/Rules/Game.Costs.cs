using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

/// <summary>Additional costs (CR 356 step b, docs/effects-fiora.md §3.1): asked after the targets, recorded in the play's
/// variables, added to the total cost, and their actions done when the play finalizes.</summary>
public sealed partial class Game
{
    private static readonly EffectVar Unpaid = new([], [], 0, false);

    /// <summary>The kill action of a cost, when it has one.</summary>
    private static KillStep? KillAction(AdditionalCost cost) => cost.Cost.Actions is [KillStep kill] ? kill : null;

    /// <summary>A card can be played only if each mandatory cost action has enough candidates.</summary>
    private bool HasAdditionalCostCandidates(PlayerId player, CardInstance card)
    {
        var context = new EffectContext { Controller = player, Source = card.Id, SourceCardId = card.CardId };
        return Effects.For(card.CardId).AdditionalCosts
            .Where(c => c.Optional != true && KillAction(c) is not null)
            .All(c => ObjectResolver.Candidates(this, context, KillAction(c)!.Target).Count >= (KillAction(c)!.Target.Count ?? 1));
    }

    /// <summary>For each cost in file order: an optional one asks whether to pay; a paid one with a kill action chooses its units.
    /// Returns true when it asked; cancelling undoes the play.</summary>
    private bool AskAdditionalCosts(PlayCardTask task)
    {
        var item = task.Item!;
        var context = item.Effect!;
        var card = State[item.Card!.Value];
        foreach (var cost in Effects.For(card.CardId).AdditionalCosts)
        {
            if (!context.Vars.TryGetValue(cost.Id, out var recorded))
            {
                if (cost.Optional == true)
                {
                    Ask(new OptionalDecision(task.Player, card.CardId, $"Pay the additional cost: {Describe(cost.Cost)}?"), (_, action) =>
                    {
                        if (action is CancelPlay)
                        {
                            UndoPlay(task);
                            return null;
                        }
                        if (action is not ChooseOptional choice) return Reject(RejectionCode.UnexpectedAction, "Answer yes or no, or cancel.");
                        context.Vars[cost.Id] = choice.Yes ? new EffectVar([], [], 1, true) : Unpaid;
                        return null;
                    });
                    return true;
                }
                recorded = context.Vars[cost.Id] = new EffectVar([], [], 1, true);
            }
            if (!recorded.Happened || KillAction(cost) is not { } kill || recorded.Objects.Count > 0) continue;
            var options = ObjectResolver.Candidates(this, context, kill.Target);
            var count = kill.Target.Count ?? 1;
            if (options.Count < count)
            {
                UndoPlay(task);
                return false;
            }
            if (options.Count == count)
            {
                context.Vars[cost.Id] = recorded with { Objects = options };
                Emit(new ChoiceMade(task.Player, "Cards", options));
                continue;
            }
            Ask(new ChooseCardsDecision(task.Player, card.CardId, options, count, count), (_, action) =>
            {
                if (action is CancelPlay)
                {
                    UndoPlay(task);
                    return null;
                }
                if (action is not ChooseCards choose) return Reject(RejectionCode.UnexpectedAction, "Choose the units to kill, or cancel.");
                if (CheckPick(choose.Cards, options, count, count, "units") is { } rejection) return rejection;
                context.Vars[cost.Id] = recorded with { Objects = [.. choose.Cards] };
                return null;
            });
            return true;
        }
        return false;
    }

    /// <summary>After payment: the paid costs' actions happen (the chosen units still on the board are killed).</summary>
    private void PayAdditionalActions(PlayCardTask task)
    {
        var context = task.Item!.Effect!;
        foreach (var cost in Effects.For(State[task.Item.Card!.Value].CardId).AdditionalCosts)
            if (context.Vars.TryGetValue(cost.Id, out var paid) && paid.Happened && KillAction(cost) is not null)
                foreach (var unit in paid.Objects.Where(id => State.Exists(id) && State[id].Place.IsLocation).ToList())
                    Kill(unit);
    }

    /// <summary>"1 energy and Body": the cost in words for the question.</summary>
    private static string Describe(Cost cost)
    {
        List<string> parts = [];
        if (cost.Energy is > 0 and var energy) parts.Add($"{energy} energy");
        parts.AddRange(cost.Power.Select(p => p.ToString()));
        if (cost.Actions is [KillStep]) parts.Add("kill a unit");
        return parts.Count == 0 ? "nothing" : string.Join(" and ", parts);
    }
}
