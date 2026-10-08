using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Effects;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Consecutive passes since the chain last changed (CR 339).</summary>
    internal int ChainPasses { get; set; }

    /// <summary>True when a triggered ability started the current chain; then focus doesn't pass when it closes (CR 346.1).</summary>
    internal bool ChainStartedByTrigger { get; set; }

    /// <summary>On a chain: when every player has passed in a row, the newest item resolves; otherwise priority moves on.</summary>
    private void PassPriority(PlayerId player)
    {
        ChainPasses++;
        if (ChainPasses >= State.Players.Count) ResolveTop();
        else State.Turn.Priority = State.Opponent(player);
    }

    /// <summary>The newest finalized item resolves. A spell the engine runs (Full or Partial) resolves automatically (spec §5.1);
    /// anything else is resolved by hand by its controller (spec §8).</summary>
    private void ResolveTop()
    {
        var item = State.Chain.Last(i => i.Status == ChainItemStatus.Finalized);
        if (item.Card is { } card && Effects.For(State[card].CardId) is { Status: not MappingStatus.Unmapped } effects)
        {
            var context = item.Effect ?? new EffectContext { Controller = item.Controller, Source = card, SourceCardId = State[card].CardId };
            Push(new ResolveEffectTask(context, SpellSteps(context.SourceCardId), g => g.AfterAutomatedResolution(item, effects)));
            return;
        }
        ResolveByHand(item, null);
    }

    /// <summary>2a hand resolution of the item, or of just the lines in <paramref name="text"/> (a Partial spell's manual lines).</summary>
    private void ResolveByHand(ChainItem item, string? text)
    {
        var cardId = item.Card is { } card ? State[card].CardId : item.SourceCardId ?? "";
        text ??= item.Text ?? (item.Card is { } c ? CardOf(c).Text.Rich : "");
        ResolvingManually = true;
        Ask(new ResolveManuallyDecision(item.Controller, item.Id, cardId, text), (_, action) =>
        {
            if (action is not ResolveDone)
                return Reject(RejectionCode.UnexpectedAction, "Carry out the effect with manual actions, then submit ResolveDone.");
            FinishResolution(item);
            return null;
        });
    }

    /// <summary>After the automated part: a Partial spell's manual lines are resolved by hand; then it finishes like any spell.</summary>
    private void AfterAutomatedResolution(ChainItem item, CardEffectInfo effects)
    {
        if (!State.Chain.Contains(item)) return;
        if (effects.ManualLines.Count > 0)
        {
            ResolveByHand(item, ManualText(item.Card!.Value, effects.ManualLines));
            return;
        }
        FinishResolution(item);
    }

    private string ManualText(ObjectId card, IReadOnlyList<int> lines)
    {
        var all = RichText.Lines(CardOf(card).Text.Rich);
        return $"<p>{string.Join("<br />", lines.Select(n => all[n - 1]))}</p>";
    }

    /// <summary>A resolved spell goes to its owner's trash; an ability is just removed.</summary>
    private void FinishResolution(ChainItem item)
    {
        State.Chain.Remove(item);
        if (item.Card is { } card && State.Exists(card) && State[card].Place.Kind == PlaceKind.Chain)
            MoveCard(card, Place.Trash(State[card].Owner));
        ResolvingManually = false;
        Emit(new ChainItemResolved(item.Id));
        MarkDirty();
        AfterResolution();
    }

    /// <summary>After an item resolves: the newest remaining item's controller gets priority, or the chain closes.</summary>
    private void AfterResolution()
    {
        ChainPasses = 0;
        if (State.Chain.Count > 0)
        {
            State.Turn.Priority = State.Chain[^1].Controller;
            return;
        }
        ChainEmptied();
    }

    /// <summary>Back to Open. In a showdown, focus passes unless a triggered ability started the chain (CR 346).</summary>
    private void ChainEmptied()
    {
        if (State.Showdown is { } showdown)
        {
            if (!ChainStartedByTrigger && State.Turn.Focus is { } focus) State.Turn.Focus = State.Opponent(focus);
            showdown.Passes = 0;
            State.Turn.Priority = State.Turn.Focus;
        }
        else
        {
            State.Turn.Priority = State.Turn.Phase == Phase.Main ? State.Turn.TurnPlayer : null;
        }
        ChainStartedByTrigger = false;
    }
}
