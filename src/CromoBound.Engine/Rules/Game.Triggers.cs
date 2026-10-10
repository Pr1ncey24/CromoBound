using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

/// <summary>Puts the pending triggered abilities on the chain (spec §5.2). It reads <see cref="Game.PendingTriggers"/> each time it
/// runs, so a rerun after a manual action also sees triggers that fired meanwhile.</summary>
internal sealed class PutTriggersOnChainTask : GameTask
{
    public override bool Run(Game game) => game.PutTriggersOnChain();
}

/// <summary>Chooses the targets of the triggered abilities that just went on the chain (CR 382-388), oldest item first: the turn
/// player's triggers went on first, so they choose first. Started at once, so cleanups and other triggers wait for the choices.
/// A trigger can't be cancelled, so it offers only the enemy Deflect units its controller can pay for, and once its targets
/// are chosen its controller pays their Deflect tax (CR 809); cancelling that payment chooses the targets again.</summary>
internal sealed class ChooseItemTargetsTask : GameTask
{
    private readonly HashSet<int> _taxPaid = [];
    private TotalCost? _adjusted;

    public override bool Run(Game game)
    {
        while (game.State.Chain.FirstOrDefault(item => NeedsTargets(item) || OwesTax(game, item)) is { } item)
        {
            if (game.AskSlotTargets(item.Controller, item.Source!.Value, item.Effect!, item, onCancel: null, taxed: true)) return false;
            if (!OwesTax(game, item)) continue;
            var effect = item.Effect!;
            var cost = _adjusted ?? new TotalCost(0, [.. Enumerable.Repeat(PowerSymbol.Any, TaxOf(game, item))]);
            game.AskPay(item.Controller, cost, game.Db.Cards[effect.SourceCardId].Domains,
                onPaid: () =>
                {
                    _taxPaid.Add(item.Id);
                    _adjusted = null;
                },
                onCancel: () =>
                {
                    effect.Targets.Clear();
                    _adjusted = null;
                },
                onAdjust: adjusted => _adjusted = adjusted);
            return false;
        }
        return true;
    }

    private static bool NeedsTargets(ChainItem item) =>
        item is { Kind: ChainItemKind.Ability, AbilityKind: AbilityKind.Triggered, Effect: { } effect } && effect.Targets.Count < effect.Slots.Count;

    /// <summary>A trigger whose targets are all chosen and whose Deflect tax isn't paid yet.</summary>
    private bool OwesTax(Game game, ChainItem item) =>
        item is { Kind: ChainItemKind.Ability, AbilityKind: AbilityKind.Triggered, Effect: { } effect }
        && effect.Slots.Count > 0 && effect.Targets.Count == effect.Slots.Count && !_taxPaid.Contains(item.Id) && TaxOf(game, item) > 0;

    private static int TaxOf(Game game, ChainItem item) =>
        Modifiers.DeflectTax(game, item.Controller, item.Effect!.Targets.SelectMany(t => t));
}

public sealed partial class Game
{
    /// <summary>The turn player's triggers go on the chain first, then the opponent's, so the opponent's resolve first. A player
    /// with several orders them; one is put on directly. Returns false while a player is ordering.</summary>
    internal bool PutTriggersOnChain()
    {
        foreach (var player in TurnOrder())
        {
            var mine = PendingTriggers.Where(t => t.Controller == player).ToList();
            if (mine.Count == 0) continue;
            if (mine.Count == 1)
            {
                AddTriggers(mine);
                continue;
            }
            List<TriggerOption> options = [.. mine.Select(t => new TriggerOption(t.Source, t.SourceCardId, AbilityText(t.SourceCardId, t.Ability)))];
            Ask(new OrderTriggersDecision(player, options), (_, action) =>
            {
                if (action is not OrderTriggers order) return Reject(RejectionCode.UnexpectedAction, "Choose the order the triggers go on the chain.");
                if (order.Order.Count != mine.Count || order.Order.Distinct().Count() != mine.Count || order.Order.Any(i => i < 0 || i >= mine.Count))
                    return Reject(RejectionCode.UnexpectedAction, $"List each of the {mine.Count} triggers once, by its index.");
                AddTriggers([.. order.Order.Select(i => mine[i])]);
                return null;
            });
            return false;
        }
        return true;
    }

    /// <summary>Each trigger becomes a finalized ability item; a chain they start doesn't pass focus when it closes (CR 346.1).
    /// A trigger with targets chooses them right after (one task, oldest item first); one whose required target
    /// has no candidate is dropped.</summary>
    private void AddTriggers(List<PendingTrigger> triggers)
    {
        List<ChainItem> targeting = [];
        foreach (var trigger in triggers)
        {
            PendingTriggers.Remove(trigger);
            var context = new EffectContext
            {
                Controller = trigger.Controller,
                Source = trigger.Source,
                SourceCardId = trigger.SourceCardId,
                Slots = TargetSlots.Of(trigger.Ability.Steps),
            };
            if (!HasSlotCandidates(context, taxed: true)) continue;
            if (State.Chain.Count == 0) ChainStartedByTrigger = true;
            var item = AddAbilityItem(trigger.Controller, trigger.Source, trigger.SourceCardId, AbilityKind.Triggered,
                AbilityText(trigger.SourceCardId, trigger.Ability), trigger.Ability.Steps, context);
            Emit(new TriggerAdded(item.Id, trigger.Source, trigger.Controller));
            if (context.Slots.Count > 0) targeting.Add(item);
        }
        if (targeting.Count > 0 && !_tasks.OfType<ChooseItemTargetsTask>().Any()) Push(new ChooseItemTargetsTask { Started = true });
    }
}
