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

    /// <summary>The newest finalized item resolves. An ability the engine runs and a spell it runs (Full or Partial) resolve
    /// automatically (spec §5.1-5.3); anything else is resolved by hand by its controller (spec §8).</summary>
    private void ResolveTop()
    {
        var item = State.Chain.Last(i => i.Status == ChainItemStatus.Finalized);
        if (item is { Kind: ChainItemKind.Ability, Steps: { } abilitySteps, Effect: { } abilityContext })
        {
            Push(new ResolveEffectTask(abilityContext, abilitySteps, g => g.FinishResolution(item), item));
            return;
        }
        if (item.Card is { } card && Effects.For(State[card].CardId) is { Status: not MappingStatus.Unmapped } effects)
        {
            var steps = SpellSteps(State[card].CardId);
            var context = item.Effect ?? new EffectContext
            {
                Controller = item.Controller,
                Source = card,
                SourceCardId = State[card].CardId,
                Slots = TargetSlots.Of(steps),
            };
            Push(new ResolveEffectTask(context, steps, g => g.AfterAutomatedResolution(item, effects), item));
            return;
        }
        ResolveByHand(item, null);
    }

    /// <summary>Puts an ability the engine runs on the chain, finalized (a trigger, an activation, a reflexive block); its
    /// controller gets priority.</summary>
    internal ChainItem AddAbilityItem(PlayerId controller, ObjectId? source, string cardId, AbilityKind kind, string? text,
        IReadOnlyList<Step> steps, EffectContext context)
    {
        var item = new ChainItem
        {
            Id = State.NextChainItemId(),
            Kind = ChainItemKind.Ability,
            Controller = controller,
            Status = ChainItemStatus.Finalized,
            Source = source,
            AbilityKind = kind,
            SourceCardId = cardId,
            Text = text,
            Effect = context,
            Steps = steps,
        };
        State.Chain.Add(item);
        Emit(new ChainItemAdded(item.Id, controller));
        MarkDirty();
        ChainPasses = 0;
        State.Turn.Priority = controller;
        return item;
    }

    /// <summary>The card text lines an ability implements, or null when its file names none (the abilities keywords stand for).</summary>
    internal string? AbilityText(string cardId, Ability ability)
    {
        if (ability.Line is not { } line) return null;
        var lines = RichText.Lines(Db.Cards[cardId].Text.Rich);
        return string.Join("<br />", line.Lines.Where(n => n >= 1 && n <= lines.Count).Select(n => lines[n - 1]));
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
