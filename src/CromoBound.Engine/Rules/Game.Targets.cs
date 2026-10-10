using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Effects.Resolvers;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>The steps of the card's spell abilities when the engine runs it (Full or Partial), in printed order; empty otherwise.</summary>
    internal IReadOnlyList<Step> SpellSteps(string cardId) =>
        Db.Cards[cardId].Type != CardType.Spell ? [] :
        [.. Effects.For(cardId).Abilities.OfType<SpellAbility>().SelectMany(a => a.Steps)];

    /// <summary>A spell the engine runs can be played only if every required target slot has enough candidates (rule 355).</summary>
    private bool HasTargetsFor(PlayerId player, CardInstance card) =>
        HasSlotCandidates(new EffectContext
        {
            Controller = player, Source = card.Id, SourceCardId = card.CardId, Slots = TargetSlots.Of(SpellSteps(card.CardId)),
        });

    /// <summary>Every slot has at least as many candidates as it requires. A <paramref name="taxed"/> check (a triggered ability)
    /// leaves out the enemy Deflect units its controller can't pay for.</summary>
    internal bool HasSlotCandidates(EffectContext context, bool taxed = false) =>
        context.Slots.All(slot => SlotCandidates(context, slot, taxed).Count >= (slot.Count ?? 0));

    /// <summary>The slot's candidates. A triggered ability can't be cancelled when it goes on the chain, so an enemy unit with
    /// Deflect X is a candidate only if its controller could pay X [A] now, on top of the tax of the targets already chosen.</summary>
    private List<ObjectId> SlotCandidates(EffectContext context, ObjectRef slot, bool taxed)
    {
        var options = ObjectResolver.Candidates(this, context, slot);
        if (!taxed) return options;
        var owed = Modifiers.DeflectTax(this, context.Controller, context.Targets.SelectMany(t => t));
        var domains = Db.Cards[context.SourceCardId].Domains;
        var pool = State.Player(context.Controller).Pool;
        var runes = RunesOf(context.Controller);
        return [.. options.Where(Affordable)];

        bool Affordable(ObjectId option)
        {
            var tax = Modifiers.DeflectTax(this, context.Controller, [option]);
            return tax == 0 || Payment.Suggest(pool, new TotalCost(0, [.. Enumerable.Repeat(PowerSymbol.Any, owed + tax)]), domains, runes) is not null;
        }
    }

    /// <summary>Spec §5.1: the spell's slots, chosen while it is played; cancelling undoes the play.</summary>
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
        if (!HasSlotCandidates(item.Effect))
        {
            UndoPlay(task);
            return false;
        }
        return AskSlotTargets(task.Player, card.Id, item.Effect, item, () => UndoPlay(task));
    }

    /// <summary>One decision per unfilled slot, in JSON order (spec §5.1). When the candidates are exactly as many as required the
    /// choice is forced: it is applied without asking and announced with <see cref="ChoiceMade"/>. Returns true when it asked. A
    /// <paramref name="taxed"/> choice (a triggered ability) offers only the Deflect units its controller can pay for.
    /// Callers check <see cref="HasSlotCandidates"/> first.</summary>
    internal bool AskSlotTargets(PlayerId player, ObjectId card, EffectContext context, ChainItem? item, Action? onCancel, bool taxed = false)
    {
        while (context.Targets.Count < context.Slots.Count)
        {
            var slot = context.Slots[context.Targets.Count];
            var options = SlotCandidates(context, slot, taxed);
            var min = Math.Min(slot.Count ?? 0, options.Count);
            var max = Math.Min(slot.Count ?? slot.UpTo ?? 0, options.Count);
            if (options.Count == min)
            {
                context.Targets.Add(options);
                Emit(new ChoiceMade(player, "Targets", options));
                if (item is not null) Emit(new TargetsChosen(item.Id, context.Targets.Count - 1, options));
                continue;
            }
            Ask(new ChooseTargetsDecision(player, card, context.Targets.Count, options, min, max), (_, action) =>
            {
                if (action is CancelPlay && onCancel is not null)
                {
                    onCancel();
                    return null;
                }
                if (action is not ChooseTargets choose)
                    return Reject(RejectionCode.UnexpectedAction, onCancel is null ? "Choose the targets." : "Choose the targets, or cancel.");
                var chosen = choose.Targets;
                if (CheckPick(chosen, options, min, max, "targets") is { } rejection) return rejection;
                context.Targets.Add([.. chosen]);
                if (item is not null) Emit(new TargetsChosen(item.Id, context.Targets.Count - 1, [.. chosen]));
                return null;
            });
            return true;
        }
        return false;
    }

    /// <summary>Null when <paramref name="chosen"/> holds between min and max different ids, all among the options.</summary>
    internal static Rejection? CheckPick(IReadOnlyList<ObjectId> chosen, IReadOnlyList<ObjectId> options, int min, int max, string what) =>
        chosen.Distinct().Count() == chosen.Count && chosen.Count >= min && chosen.Count <= max && chosen.All(options.Contains)
            ? null
            : Reject(RejectionCode.InvalidTarget, $"Choose between {min} and {max} different {what} among the offered ones.");
}
