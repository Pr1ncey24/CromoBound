using CromoBound.Engine.Actions;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Moves a card. A move between two hidden places emits a private event for the owner (or the facedown card's
    /// controller) plus an anonymous public one; any other move is public.</summary>
    internal ObjectId? MoveCard(ObjectId id, Place to, DeckPosition position = DeckPosition.Top)
    {
        var instance = State[id];
        var from = instance.Place;
        var cardId = instance.CardId;
        var viewer = from.Kind == PlaceKind.Facedown ? instance.Controller : instance.Owner;
        var newId = State.Move(id, to, position);
        var landed = newId is { } moved ? State[moved].Place : to;
        if (IsHidden(from) && IsHidden(landed))
        {
            Emit(new CardMoved(cardId, id, newId, from, landed) { VisibleTo = viewer });
            Emit(new CardMoved(null, null, null, from, landed));
        }
        else
        {
            Emit(new CardMoved(cardId, id, newId, from, landed));
        }
        MarkDirty();
        return newId;
    }

    private static bool IsHidden(Place place) =>
        place.Kind is PlaceKind.Hand or PlaceKind.MainDeck or PlaceKind.RuneDeck or PlaceKind.Facedown;

    internal void SetStatus(ObjectId id, StatusKind status, bool value)
    {
        var instance = State[id];
        var current = status switch
        {
            StatusKind.Exhausted => instance.Exhausted,
            StatusKind.Stunned => instance.Stunned,
            StatusKind.Buffed => instance.Buffed,
            _ => instance.Empowered,
        };
        if (current == value) return;
        switch (status)
        {
            case StatusKind.Exhausted: instance.Exhausted = value; break;
            case StatusKind.Stunned: instance.Stunned = value; break;
            case StatusKind.Buffed: instance.Buffed = value; break;
            default: instance.Empowered = value; break;
        }
        Emit(new StatusChanged(id, status, value));
        MarkDirty();
    }

    internal void DealDamage(ObjectId unit, int amount)
    {
        if (amount <= 0) return;
        State[unit].Damage += amount;
        Emit(new DamageDealt(unit, amount));
        MarkDirty();
    }

    internal void HealAllUnits()
    {
        var damaged = BoardUnits().Where(u => u.Damage > 0).ToList();
        if (damaged.Count == 0) return;
        foreach (var unit in damaged) unit.Damage = 0;
        Emit(new UnitsHealed());
        MarkDirty();
    }

    /// <summary>Points never go below 0 (CR 194.4).</summary>
    internal void GainPoints(PlayerId player, int amount)
    {
        var state = State.Player(player);
        var points = Math.Max(0, state.Points + amount);
        if (points == state.Points) return;
        state.Points = points;
        Emit(new PointsChanged(player, points));
        MarkDirty();
    }

    internal void Kill(ObjectId unit)
    {
        var instance = State[unit];
        Emit(new UnitDied(unit, instance.CardId, instance.Controller));
        MoveCard(unit, Place.Trash(instance.Owner));
    }

    /// <summary>Returns a permanent to its controller's Base. Not a move (CR 454): damage and statuses stay.</summary>
    internal void Recall(ObjectId id) => MoveCard(id, Place.Base(State[id].Controller));

    /// <summary>Draws one card at a time; an empty Main Deck burns out first (CR 413, 431).</summary>
    internal void Draw(PlayerId player, int count)
    {
        var streak = 0;
        for (var i = 0; i < count && Outcome is null; i++)
        {
            if (State.At(Place.MainDeck(player)).Count == 0)
            {
                BurnOut(player, ++streak);
                if (State.At(Place.MainDeck(player)).Count == 0) continue;
            }
            MoveCard(State.At(Place.MainDeck(player))[0], Place.Hand(player));
            streak = 0;
        }
    }

    /// <summary>CR 431: the trash is shuffled into the Main Deck and the opponent gains 1 point. From the second burnout in a row,
    /// a point that reaches the Victory Score with the lead wins at once.</summary>
    internal void BurnOut(PlayerId player, int streak)
    {
        foreach (var card in State.At(Place.Trash(player)).ToList()) MoveCard(card, Place.MainDeck(player), DeckPosition.Bottom);
        State.Shuffle(Place.MainDeck(player));
        var opponent = State.Opponent(player);
        Emit(new BurnedOut(player, opponent));
        GainPoints(opponent, 1);
        if (streak >= 2 && HasWon(opponent)) End(opponent, GameEndReason.BurnOut);
    }

    /// <summary>Top runes of the Rune Deck to the Base, ready; as many as remain (CR 430).</summary>
    internal void Channel(PlayerId player, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var deck = State.At(Place.RuneDeck(player));
            if (deck.Count == 0) return;
            MoveCard(deck[0], Place.Base(player));
        }
    }

    /// <summary>A rune's Reaction Add (CR 429): exhaust for 1 energy, or recycle to the bottom of the Rune Deck for 1 power of its domain.</summary>
    internal void ApplyRune(PlayerId player, ObjectId rune, RuneUse use)
    {
        var pool = State.Player(player).Pool;
        if (use == RuneUse.Exhaust)
        {
            SetStatus(rune, StatusKind.Exhausted, true);
            pool.Energy++;
            Emit(new ResourcesAdded(player, 1, null));
            return;
        }
        var domain = CardOf(rune).Domains[0];
        MoveCard(rune, Place.RuneDeck(player), DeckPosition.Bottom);
        pool.AddPower(domain);
        Emit(new ResourcesAdded(player, 0, domain));
    }

    /// <summary>CR 470-471: one score per battlefield per player per turn. A Conquer for the final point needs every battlefield
    /// scored this turn (including this one); otherwise the player draws a card instead.</summary>
    internal void Score(PlayerId player, int battlefield, ScoreKind kind)
    {
        if (State.Turn.HasScored(player, battlefield)) return;
        State.Turn.MarkScored(player, battlefield);
        var finalPoint = State.Player(player).Points >= VictoryScore - 1;
        var allScored = State.Battlefields.All(b => State.Turn.HasScored(player, b.Index));
        var gains = kind == ScoreKind.Hold || !finalPoint || allScored;
        Emit(new BattlefieldScored(player, battlefield, kind, gains));
        if (gains) GainPoints(player, 1);
        else Draw(player, 1);
    }

    /// <summary>The player takes control (clearing Contested). Newly gaining control is a Conquer if not scored this turn.</summary>
    internal void EstablishControl(PlayerId player, int battlefield)
    {
        var state = State.Battlefields[battlefield];
        state.ContestedBy = null;
        MarkDirty();
        if (state.Controller == player) return;
        state.Controller = player;
        Emit(new ControlChanged(battlefield, player));
        Score(player, battlefield, ScoreKind.Conquer);
    }

    internal void LoseControl(int battlefield)
    {
        var state = State.Battlefields[battlefield];
        if (state.Controller is null) return;
        state.Controller = null;
        Emit(new ControlChanged(battlefield, null));
        MarkDirty();
    }
}
