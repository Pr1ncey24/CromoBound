using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>The steps of the card's spell abilities when the engine runs it (Full or Partial), in printed order; empty otherwise.</summary>
    internal IReadOnlyList<Step> SpellSteps(string cardId) =>
        [.. Effects.For(cardId).Abilities.OfType<SpellAbility>().SelectMany(a => a.Steps)];

    /// <summary>A spell the engine runs can be played only if every required target slot has enough candidates (rule 355).</summary>
    private bool HasTargetsFor(PlayerId player, CardInstance card)
    {
        var slots = TargetSlots.Of(SpellSteps(card.CardId));
        if (slots.Count == 0) return true;
        var context = new EffectContext { Controller = player, Source = card.Id, SourceCardId = card.CardId, Slots = slots };
        return slots.All(slot => ObjectResolver.Candidates(this, context, slot).Count >= (slot.Count ?? 0));
    }

    /// <summary>Spec §5.1: one decision per target slot, in JSON order. When the candidates are exactly as many as required the
    /// choice is forced: it is applied without asking and announced with <see cref="ChoiceMade"/>. Returns true when it asked.</summary>
    private bool AskTargets(PlayCardTask task)
    {
        var item = task.Item!;
        var card = State[item.Card!.Value];
        item.Effect ??= new EffectContext
        {
            Controller = task.Player,
            Source = card.Id,
            SourceCardId = card.CardId,
            Slots = TargetSlots.Of(SpellSteps(card.CardId)),
        };
        var context = item.Effect;
        while (context.Targets.Count < context.Slots.Count)
        {
            var slot = context.Slots[context.Targets.Count];
            var options = ObjectResolver.Candidates(this, context, slot);
            var min = slot.Count ?? 0;
            var max = Math.Min(slot.Count ?? slot.UpTo ?? 0, options.Count);
            if (options.Count < min)
            {
                UndoPlay(task);
                return false;
            }
            if (options.Count == min)
            {
                context.Targets.Add(options);
                Emit(new ChoiceMade(task.Player, "Targets", options));
                Emit(new TargetsChosen(item.Id, context.Targets.Count - 1, options));
                continue;
            }
            Ask(new ChooseTargetsDecision(task.Player, card.Id, context.Targets.Count, options, min, max), (_, action) =>
            {
                if (action is CancelPlay)
                {
                    UndoPlay(task);
                    return null;
                }
                if (action is not ChooseTargets choose) return Reject(RejectionCode.UnexpectedAction, "Choose the targets, or cancel.");
                var chosen = choose.Targets;
                if (chosen.Distinct().Count() != chosen.Count || chosen.Count < min || chosen.Count > max || !chosen.All(options.Contains))
                    return Reject(RejectionCode.InvalidTarget, $"Choose between {min} and {max} different targets among the offered ones.");
                context.Targets.Add([.. chosen]);
                Emit(new TargetsChosen(item.Id, context.Targets.Count - 1, [.. chosen]));
                return null;
            });
            return true;
        }
        return false;
    }
}
