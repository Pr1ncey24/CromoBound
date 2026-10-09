using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;

namespace CromoBound.Engine.Rules;

/// <summary>Puts the pending triggered abilities on the chain (spec §5.2). It reads <see cref="Game.PendingTriggers"/> each time it
/// runs, so a rerun after a manual action also sees triggers that fired meanwhile.</summary>
internal sealed class PutTriggersOnChainTask : GameTask
{
    public override bool Run(Game game) => game.PutTriggersOnChain();
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

    /// <summary>Each trigger becomes a finalized ability item; a chain they start doesn't pass focus when it closes (CR 346.1).</summary>
    private void AddTriggers(List<PendingTrigger> triggers)
    {
        foreach (var trigger in triggers)
        {
            PendingTriggers.Remove(trigger);
            if (State.Chain.Count == 0) ChainStartedByTrigger = true;
            var context = new EffectContext { Controller = trigger.Controller, Source = trigger.Source, SourceCardId = trigger.SourceCardId };
            var item = AddAbilityItem(trigger.Controller, trigger.Source, trigger.SourceCardId, AbilityKind.Triggered,
                AbilityText(trigger.SourceCardId, trigger.Ability), trigger.Ability.Steps, context);
            Emit(new TriggerAdded(item.Id, trigger.Source, trigger.Controller));
        }
    }
}
