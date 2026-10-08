using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Engine.Rules;

/// <summary>An ability added by hand: pay its cost (if any), then it's finalized and its controller gets priority.</summary>
internal sealed class AbilityTask(ChainItem item, TotalCost? cost) : GameTask
{
    public ChainItem Item { get; } = item;
    public TotalCost? Cost { get; set; } = cost;
    public bool Paid { get; set; } = cost is null;
    public bool Cancelled { get; set; }

    public override bool Run(Game game) => game.RunAbility(this);
}

public sealed partial class Game
{
    /// <summary>Applies a manual action (spec §8), then lets the rules catch up: the pending decision is asked again with fresh
    /// options (a hand resolution stays as it is) and cleanup runs.</summary>
    public SubmitResult SubmitManual(PlayerId player, ManualAction action)
    {
        if (Outcome is not null) return SubmitResult.Reject(RejectionCode.MatchOver, "The game is over.");
        if (ApplyManual(player, action) is { } rejection) return new SubmitResult(false, rejection, []);
        Emit(new ManualActionTaken(player, action));
        if (Pending is not ResolveManuallyDecision)
        {
            Pending = null;
            _handler = null;
        }
        return new SubmitResult(true, null, Continue());
    }

    private bool IsPlayer(PlayerId player) => player.Index >= 0 && player.Index < State.Players.Count;

    private CardInstance? OnBoard(ObjectId id) => State.Exists(id) && State[id].Place.IsLocation ? State[id] : null;

    private CardInstance? BoardUnit(ObjectId id) => OnBoard(id) is { } instance && IsUnit(instance) ? instance : null;

    private static bool IsDeck(PlaceKind kind) => kind is PlaceKind.MainDeck or PlaceKind.RuneDeck;

    private Rejection? ApplyManual(PlayerId player, ManualAction action)
    {
        switch (action)
        {
            case ManualMoveCard move:
                return ManualMove(move);
            case ManualDamage damage:
                if (BoardUnit(damage.Unit) is null || damage.Amount <= 0) return Reject(RejectionCode.UnknownObject, "Choose a unit in play and a positive amount.");
                DealDamage(damage.Unit, damage.Amount);
                return null;
            case ManualHeal heal:
                if (BoardUnit(heal.Unit) is not { } wounded || heal.Amount <= 0) return Reject(RejectionCode.UnknownObject, "Choose a unit in play and a positive amount.");
                var healed = Math.Min(heal.Amount, wounded.Damage);
                wounded.Damage -= healed;
                Emit(new UnitHealed(heal.Unit, healed));
                MarkDirty();
                return null;
            case ManualSetStatus status:
                if (OnBoard(status.Card) is null) return Reject(RejectionCode.UnknownObject, "Choose a card in play.");
                SetStatus(status.Card, status.Status, status.Value);
                return null;
            case ManualModifyMight might:
                if (BoardUnit(might.Unit) is not { } target) return Reject(RejectionCode.UnknownObject, "Choose a unit in play.");
                target.Modifiers.Add(new MightModifier(might.Amount, might.Duration));
                Emit(new MightModified(might.Unit, might.Amount, might.Duration));
                MarkDirty();
                return null;
            case ManualAdjustPoints points:
                if (!IsPlayer(points.Player)) return Reject(RejectionCode.UnknownObject, "No such player.");
                GainPoints(points.Player, points.Amount);
                return null;
            case ManualAdjustXp xp:
                if (!IsPlayer(xp.Player)) return Reject(RejectionCode.UnknownObject, "No such player.");
                var holder = State.Player(xp.Player);
                holder.Xp = Math.Max(0, holder.Xp + xp.Amount);
                Emit(new XpChanged(xp.Player, holder.Xp));
                return null;
            case ManualAdjustPool pool:
                return AdjustPool(pool);
            case ManualCreateToken token:
                return CreateToken(token);
            case ManualGainControl control:
                if (OnBoard(control.Card) is null || !IsPlayer(control.Player)) return Reject(RejectionCode.UnknownObject, "Choose a card in play and a player.");
                State[control.Card].Controller = control.Player;
                Emit(new ControlGained(control.Card, control.Player));
                MarkDirty();
                return null;
            case ManualShuffle shuffle:
                if (!IsPlayer(shuffle.Owner) || !IsDeck(shuffle.Deck)) return Reject(RejectionCode.UnknownObject, "Choose a player's Main Deck or Rune Deck.");
                State.Shuffle(new Place(shuffle.Deck, shuffle.Owner, null));
                Emit(new DeckShuffled(shuffle.Owner, shuffle.Deck));
                return null;
            case ManualLookAtTop look:
                return LookAtTop(player, look);
            case ManualReveal reveal:
                if (!State.Exists(reveal.Card)) return Reject(RejectionCode.UnknownObject, $"{reveal.Card} doesn't exist.");
                Emit(new CardRevealed(reveal.Card, State[reveal.Card].CardId));
                return null;
            case ManualCounter counter:
                return Counter(counter.ChainItem);
            case AddAbilityToChain ability:
                return AddAbility(ability);
            default:
                return Reject(RejectionCode.UnexpectedAction, $"{action.GetType().Name} is not a manual action.");
        }
    }

    /// <summary>Moves a card anywhere except the chain, battlefield cards and the Legend Zone. A unit arriving at a battlefield
    /// applies Contested; a card put face down can be played from the next turn.</summary>
    private Rejection? ManualMove(ManualMoveCard move)
    {
        if (!State.Exists(move.Card)) return Reject(RejectionCode.UnknownObject, $"{move.Card} doesn't exist.");
        var to = move.Destination;
        if (State[move.Card].Place.Kind is PlaceKind.Chain or PlaceKind.BattlefieldCard or PlaceKind.LegendZone
            || to.Kind is PlaceKind.Chain or PlaceKind.BattlefieldCard or PlaceKind.LegendZone)
            return Reject(RejectionCode.IllegalLocation, "Cards on the chain, battlefields and legends can't be moved by hand (use ManualCounter for the chain).");
        if ((to.Kind is PlaceKind.Battlefield or PlaceKind.Facedown) && (to.Index is not { } index || index < 0 || index >= State.Battlefields.Count))
            return Reject(RejectionCode.IllegalLocation, "No such battlefield.");
        if ((to.IsPlayerPile || to.Kind == PlaceKind.Base) && (to.Player is not { } owner || !IsPlayer(owner)))
            return Reject(RejectionCode.IllegalLocation, "Name the player whose zone it is.");

        if (MoveCard(move.Card, to, move.Position) is not { } moved) return null;
        var card = State[moved];
        if (to.Kind == PlaceKind.Facedown)
        {
            card.Facedown = true;
            card.HiddenOnTurn = State.Turn.Number;
        }
        if (to.Kind == PlaceKind.Battlefield && IsUnit(card)) ApplyContested(card.Controller, to.Index!.Value);
        return null;
    }

    private Rejection? AdjustPool(ManualAdjustPool adjust)
    {
        if (!IsPlayer(adjust.Player)) return Reject(RejectionCode.UnknownObject, "No such player.");
        if (adjust.Power != 0 && adjust.Domain is null) return Reject(RejectionCode.UnexpectedAction, "Name the domain of the power.");
        var pool = State.Player(adjust.Player).Pool;
        pool.Energy = Math.Max(0, pool.Energy + adjust.Energy);
        pool.UniversalPower = Math.Max(0, pool.UniversalPower + adjust.UniversalPower);
        if (adjust.Domain is { } domain) pool.Power[domain] = Math.Max(0, pool.Power.GetValueOrDefault(domain) + adjust.Power);
        Emit(new PoolAdjusted(adjust.Player));
        return null;
    }

    /// <summary>The creator owns and controls the token (CR 183); it enters at a Base or a battlefield.</summary>
    private Rejection? CreateToken(ManualCreateToken token)
    {
        if (!Db.Cards.TryGetValue(token.TokenId, out var card) || card.Supertype != Supertype.Token)
            return Reject(RejectionCode.UnknownObject, $"'{token.TokenId}' is not a token.");
        var location = token.Location;
        var valid = IsPlayer(token.Controller) && location.Kind switch
        {
            PlaceKind.Base => location.Player is { } owner && IsPlayer(owner),
            PlaceKind.Battlefield => location.Index is { } index && index >= 0 && index < State.Battlefields.Count,
            _ => false,
        };
        if (!valid) return Reject(RejectionCode.IllegalLocation, "Tokens enter at a Base or a battlefield.");
        var id = State.Create(token.TokenId, null, token.Controller, location, isToken: true);
        Emit(new TokenCreated(id, token.TokenId, location));
        MarkDirty();
        if (location.Kind == PlaceKind.Battlefield && card.Type == CardType.Unit) ApplyContested(token.Controller, location.Index!.Value);
        return null;
    }

    /// <summary>Only the looker sees which cards; everyone else sees how many.</summary>
    private Rejection? LookAtTop(PlayerId looker, ManualLookAtTop look)
    {
        if (!IsPlayer(look.Owner) || !IsDeck(look.Deck) || look.Count < 1)
            return Reject(RejectionCode.UnknownObject, "Choose a player's Main Deck or Rune Deck and how many cards.");
        var top = State.At(new Place(look.Deck, look.Owner, null)).Take(look.Count).Select(id => State[id].CardId).ToList();
        Emit(new CardsLookedAt(looker, look.Owner, look.Deck, top.Count, top) { VisibleTo = looker });
        Emit(new CardsLookedAt(looker, look.Owner, look.Deck, top.Count, null));
        return null;
    }

    /// <summary>Removes a finalized chain item; its card goes to its owner's trash (CR 425). Countering the item being
    /// resolved by hand ends that resolution.</summary>
    private Rejection? Counter(int itemId)
    {
        var item = State.Chain.FirstOrDefault(i => i.Id == itemId);
        if (item is null) return Reject(RejectionCode.UnknownObject, $"There is no chain item {itemId}.");
        if (item.Status != ChainItemStatus.Finalized) return Reject(RejectionCode.WrongTiming, "Only finalized chain items can be countered.");
        State.Chain.Remove(item);
        if (item.Card is { } card && State.Exists(card)) MoveCard(card, Place.Trash(State[card].Owner));
        Emit(new ChainItemCountered(itemId));
        MarkDirty();
        if (Pending is ResolveManuallyDecision resolving && resolving.ChainItem == itemId)
        {
            ResolvingManually = false;
            Pending = null;
            _handler = null;
        }
        AfterResolution();
        return null;
    }

    /// <summary>Puts line <c>Line</c> of the source's text on the chain as a pending ability controlled by the source's controller.</summary>
    private Rejection? AddAbility(AddAbilityToChain add)
    {
        if (!State.Exists(add.Source)) return Reject(RejectionCode.UnknownObject, $"{add.Source} doesn't exist.");
        var source = State[add.Source];
        var lines = RichText.Lines(CardOf(source).Text.Rich);
        if (add.Line < 1 || add.Line > lines.Count) return Reject(RejectionCode.UnexpectedAction, $"Line {add.Line} doesn't exist on that card.");
        if (State.Chain.Count == 0) ChainStartedByTrigger = add.Kind == AbilityKind.Triggered;
        var item = new ChainItem
        {
            Id = State.NextChainItemId(),
            Kind = ChainItemKind.Ability,
            Controller = source.Controller,
            Source = add.Source,
            TextLine = add.Line,
            AbilityKind = add.Kind,
            SourceCardId = source.CardId,
            Text = lines[add.Line - 1],
        };
        State.Chain.Add(item);
        Emit(new ChainItemAdded(item.Id, item.Controller));
        MarkDirty();
        Push(new AbilityTask(item, add.Cost));
        return null;
    }

    internal bool RunAbility(AbilityTask task)
    {
        if (task.Cancelled) return true;
        if (!task.Paid)
        {
            AskPay(task.Item.Controller, task.Cost!, Db.Cards[task.Item.SourceCardId!].Domains,
                onPaid: () => task.Paid = true,
                onCancel: () =>
                {
                    State.Chain.Remove(task.Item);
                    Emit(new PlayCancelled(task.Item.Id));
                    task.Cancelled = true;
                },
                onAdjust: adjusted => task.Cost = adjusted);
            return false;
        }
        task.Item.Status = ChainItemStatus.Finalized;
        ChainPasses = 0;
        State.Turn.Priority = task.Item.Controller;
        return true;
    }
}
