using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

internal enum PlayStep { ToChain, Choices, Targets, Cost, Pay, Finalize, Cancelled }

/// <summary>Playing a card, CR 353-359: to the chain, choices, total cost, payment, finalize.</summary>
internal sealed class PlayCardTask(PlayerId player, ObjectId source) : GameTask
{
    public PlayerId Player { get; } = player;
    public ObjectId Source { get; } = source;
    public PlayStep Step { get; set; }
    public ChainItem? Item { get; set; }
    public Place Origin { get; set; }
    public int? HiddenOnTurn { get; set; }
    public TotalCost Cost { get; set; } = new(0, []);
    public bool FromHidden => Origin.Kind == PlaceKind.Facedown;

    public override bool Run(Game game) => game.RunPlay(this);
}

public sealed partial class Game
{
    private void StartPlay(PlayerId player, ObjectId card) => Push(new PlayCardTask(player, card));

    internal bool RunPlay(PlayCardTask task)
    {
        while (true)
        {
            switch (task.Step)
            {
                case PlayStep.ToChain:
                    PutOnChain(task);
                    task.Step = PlayStep.Choices;
                    break;
                case PlayStep.Choices:
                    if (AskPlayChoices(task)) return false;
                    task.Step = PlayStep.Targets;
                    break;
                case PlayStep.Targets:
                    if (AskTargets(task)) return false;
                    if (task.Step == PlayStep.Targets) task.Step = PlayStep.Cost;
                    break;
                case PlayStep.Cost:
                    task.Cost = Payment.CostOf(CardOf(task.Item!.Card!.Value), task.FromHidden, task.Item.Accelerate);
                    task.Step = PlayStep.Pay;
                    break;
                case PlayStep.Pay:
                    AskPay(task.Player, task.Cost, CardOf(task.Item!.Card!.Value).Domains,
                        onPaid: () => task.Step = PlayStep.Finalize,
                        onCancel: () => UndoPlay(task),
                        onAdjust: adjusted => task.Cost = adjusted);
                    return false;
                case PlayStep.Finalize:
                    FinishFinalizing(task);
                    return true;
                default:
                    return true;
            }
        }
    }

    private void PutOnChain(PlayCardTask task)
    {
        var source = State[task.Source];
        task.Origin = source.Place;
        task.HiddenOnTurn = source.HiddenOnTurn;
        if (State.Chain.Count == 0) ChainStartedByTrigger = false;
        var item = new ChainItem { Id = State.NextChainItemId(), Kind = ChainItemKind.Card, Controller = task.Player };
        item.Card = MoveCard(task.Source, Place.Chain, DeckPosition.Bottom);
        State[item.Card!.Value].Controller = task.Player;
        State.Chain.Add(item);
        Emit(new ChainItemAdded(item.Id, task.Player));
        task.Item = item;
    }

    /// <summary>Where the permanent may enter (CR 355.2.a, 811.1.d): a unit at your Base or a battlefield you control;
    /// gear at your Base; from Hidden, that card's battlefield. Spells have no location.</summary>
    private List<Place> PlayLocations(PlayCardTask task)
    {
        var card = CardOf(task.Item!.Card!.Value);
        if (card.Type == CardType.Spell) return [];
        if (task.FromHidden) return [Place.Battlefield(task.Origin.Index!.Value)];
        if (card.Type != CardType.Unit) return [Place.Base(task.Player)];
        return [Place.Base(task.Player), .. State.Battlefields.Where(b => b.Controller == task.Player).Select(b => Place.Battlefield(b.Index))];
    }

    /// <summary>Asks for the location and Accelerate when there is a real choice; returns true when it asked.</summary>
    private bool AskPlayChoices(PlayCardTask task)
    {
        var item = task.Item!;
        var locations = PlayLocations(task);
        var accelerate = CardOf(item.Card!.Value).Type == CardType.Unit && Has(State[item.Card.Value], DisplayKeyword.Accelerate);
        if (locations.Count <= 1 && !accelerate)
        {
            item.Location = locations.Count == 1 ? locations[0] : null;
            return false;
        }
        Ask(new PlayChoicesDecision(task.Player, item.Card.Value, locations, accelerate), (_, action) =>
        {
            if (action is CancelPlay)
            {
                UndoPlay(task);
                return null;
            }
            if (action is not ChoosePlayOptions choice)
                return Reject(RejectionCode.UnexpectedAction, "Choose where the card enters, or cancel.");
            var location = choice.Location ?? (locations.Count == 1 ? locations[0] : null);
            if (location is null || !locations.Contains(location.Value))
                return Reject(RejectionCode.IllegalLocation, "Choose one of the offered locations.");
            if (choice.Accelerate && !accelerate)
                return Reject(RejectionCode.UnexpectedAction, "This card has no Accelerate.");
            item.Location = location;
            item.Accelerate = choice.Accelerate;
            task.Step = PlayStep.Targets;
            return null;
        });
        return true;
    }

    /// <summary>Asks for payment (spec §7.4 step 4). AdjustCost changes the cost and asks again; CancelPlay undoes the play.</summary>
    internal void AskPay(PlayerId player, TotalCost cost, IReadOnlyList<Domain> domains, Action onPaid, Action onCancel, Action<TotalCost> onAdjust)
    {
        var suggestion = Payment.Suggest(State.Player(player).Pool, cost, domains, RunesOf(player));
        Ask(new PayCostDecision(player, cost, domains, suggestion), (_, action) =>
        {
            switch (action)
            {
                case CancelPlay:
                    onCancel();
                    return null;
                case AdjustCost adjust:
                    var adjusted = Payment.Adjust(cost, adjust.Energy, adjust.AddPower, adjust.RemovePower);
                    Emit(new CostAdjusted(player, adjusted));
                    onAdjust(adjusted);
                    return null;
                case PayCost pay:
                    if (ValidatePayment(player, pay, cost, domains) is { } rejection) return rejection;
                    foreach (var rune in pay.Exhaust) ApplyRune(player, rune, RuneUse.Exhaust);
                    foreach (var rune in pay.Recycle) ApplyRune(player, rune, RuneUse.Recycle);
                    if (!Payment.TryPay(State.Player(player).Pool, cost, domains))
                        throw new InvalidOperationException("A validated payment failed.");
                    onPaid();
                    return null;
                default:
                    return Reject(RejectionCode.UnexpectedAction, "Pay the cost, adjust it, or cancel.");
            }
        });
    }

    private Rejection? ValidatePayment(PlayerId player, PayCost pay, TotalCost cost, IReadOnlyList<Domain> domains)
    {
        if (pay.Exhaust.Distinct().Count() != pay.Exhaust.Count || pay.Recycle.Distinct().Count() != pay.Recycle.Count)
            return Reject(RejectionCode.InsufficientPayment, "A rune can be exhausted once and recycled once.");
        var runes = RunesOf(player).ToDictionary(r => r.Id);
        foreach (var id in pay.Exhaust.Concat(pay.Recycle))
            if (!runes.ContainsKey(id)) return Reject(RejectionCode.UnknownObject, $"{id} is not a rune in your Base.");
        if (pay.Exhaust.Any(id => runes[id].Exhausted))
            return Reject(RejectionCode.InsufficientPayment, "An exhausted rune can't be exhausted again.");
        var pool = State.Player(player).Pool.Clone();
        pool.Energy += pay.Exhaust.Count;
        foreach (var id in pay.Recycle) pool.AddPower(runes[id].Domain);
        return Payment.TryPay(pool, cost, domains) ? null : Reject(RejectionCode.InsufficientPayment, "Those runes and your pool don't cover the cost.");
    }

    /// <summary>The card goes back where it came from (face down again if it was hidden); nothing was spent.</summary>
    private void UndoPlay(PlayCardTask task)
    {
        var item = task.Item!;
        State.Chain.Remove(item);
        var back = MoveCard(item.Card!.Value, task.Origin, DeckPosition.Bottom);
        if (task.FromHidden && back is { } id)
        {
            var card = State[id];
            card.Facedown = true;
            card.Controller = task.Player;
            card.HiddenOnTurn = task.HiddenOnTurn;
        }
        Emit(new PlayCancelled(item.Id));
        task.Step = PlayStep.Cancelled;
    }

    /// <summary>CR 359: a permanent leaves the chain and enters the board (a unit exhausted unless Accelerated, gear ready);
    /// a spell stays on the chain, finalized, and its controller gets priority (CR 337.4).</summary>
    private void FinishFinalizing(PlayCardTask task)
    {
        var item = task.Item!;
        var card = CardOf(item.Card!.Value);
        item.Status = ChainItemStatus.Finalized;
        ChainPasses = 0;
        if (card.Type is CardType.Unit or CardType.Gear)
        {
            State.Chain.Remove(item);
            var id = MoveCard(item.Card.Value, item.Location!.Value)!.Value;
            State[id].Controller = task.Player;
            if (card.Type == CardType.Unit && !item.Accelerate) SetStatus(id, StatusKind.Exhausted, true);
            Emit(new ChainItemResolved(item.Id));
            AfterResolution();
            return;
        }
        State.Turn.Priority = item.Controller;
    }
}
