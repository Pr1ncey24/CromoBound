using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Rules;

public sealed partial class Game
{
    /// <summary>Closed while a chain exists (CR 309).</summary>
    internal bool IsClosed => State.Chain.Count > 0;

    /// <summary>Asks whoever may act now (spec §7.2): the priority holder on a chain, the focus holder in a showdown,
    /// else the turn player in the Main phase.</summary>
    private void AskPriority()
    {
        var turn = State.Turn;
        PlayerId player;
        if (IsClosed) player = turn.Priority ?? throw new InvalidOperationException("A chain exists but nobody has priority.");
        else if (State.Showdown is not null) player = turn.Focus ?? throw new InvalidOperationException("A showdown is running but nobody has focus.");
        else if (turn.Phase == Phase.Main) player = turn.TurnPlayer;
        else throw new InvalidOperationException($"Nothing left to do in the {turn.Phase} phase.");
        turn.Priority = player;
        Ask(PriorityOptions(player), HandlePriority);
    }

    internal PriorityDecision PriorityOptions(PlayerId player)
    {
        var neutralOpenMain = IsNeutralOpenMain(player);
        return new PriorityDecision(
            player,
            [.. PlayableCards(player)],
            [.. RunesOf(player).Select(r => new RuneOption(r.Id, !r.Exhausted))],
            neutralOpenMain ? [.. MoveOptions(player)] : [],
            neutralOpenMain ? [.. HideOptions(player)] : [],
            [.. ActivateOptions(player)],
            CanPass: IsClosed || State.Showdown is not null,
            CanEndTurn: neutralOpenMain && State.StagedShowdowns.Count == 0 && State.StagedCombats.Count == 0);
    }

    private bool IsNeutralOpenMain(PlayerId player) =>
        !IsClosed && State.Showdown is null && State.Turn.Phase == Phase.Main && State.Turn.TurnPlayer == player;

    /// <summary>Cards the player may start playing now, by timing only (CR 310, 806, 811, 813). Payment is checked later.</summary>
    private IEnumerable<ObjectId> PlayableCards(PlayerId player)
    {
        var anything = IsNeutralOpenMain(player);
        foreach (var id in State.At(Place.Hand(player)).Concat(State.At(Place.ChampionZone(player))))
        {
            var card = State[id];
            var timing = anything
                || Has(card, DisplayKeyword.Reaction)
                || (!IsClosed && Has(card, DisplayKeyword.Action));
            if (timing && HasTargetsFor(player, card)) yield return id;
        }
        foreach (var battlefield in State.Battlefields)
            foreach (var id in State.At(Place.Facedown(battlefield.Index)))
                if (CanPlayFromHidden(State[id], player) && HasTargetsFor(player, State[id])) yield return id;
    }

    /// <summary>A hidden card gains Reaction from the turn after it was hidden (CR 811.1.b).</summary>
    private bool CanPlayFromHidden(CardInstance card, PlayerId player) =>
        card.Controller == player && card.HiddenOnTurn is { } hiddenOn && State.Turn.Number > hiddenOn;

    /// <summary>Standard Move (CR 144): a ready unit from Base to a battlefield, from a battlefield back to Base,
    /// or between battlefields with Ganking.</summary>
    private IEnumerable<MoveOption> MoveOptions(PlayerId player)
    {
        foreach (var unit in BoardUnits().Where(u => u.Controller == player && !u.Exhausted))
        {
            var destinations = new List<Place>();
            if (unit.Place.Kind == PlaceKind.Base)
            {
                destinations.AddRange(State.Battlefields.Select(b => Place.Battlefield(b.Index)));
            }
            else
            {
                destinations.Add(Place.Base(player));
                if (Has(unit, DisplayKeyword.Ganking))
                    destinations.AddRange(State.Battlefields.Where(b => b.Index != unit.Place.Index).Select(b => Place.Battlefield(b.Index)));
            }
            yield return new MoveOption(unit.Id, destinations);
        }
    }

    /// <summary>Hidden cards in hand, and battlefields the player controls with a free facedown slot (CR 421, 811).</summary>
    private IEnumerable<HideOption> HideOptions(PlayerId player)
    {
        var battlefields = State.Battlefields
            .Where(b => b.Controller == player && State.At(Place.Facedown(b.Index)).Count < BattlefieldState.FacedownCapacity)
            .Select(b => b.Index)
            .ToList();
        if (battlefields.Count == 0) yield break;
        foreach (var id in State.At(Place.Hand(player)))
            if (Has(State[id], DisplayKeyword.Hidden)) yield return new HideOption(id, battlefields);
    }

    private Rejection? UseRuneNow(PlayerId player, UseRune action)
    {
        var rune = RunesOf(player).FirstOrDefault(r => r.Id == action.Rune);
        if (rune is null) return Reject(RejectionCode.UnknownObject, $"{action.Rune} is not a rune in your Base.");
        if (action.Use == RuneUse.Exhaust && rune.Exhausted) return Reject(RejectionCode.WrongTiming, "That rune is already exhausted.");
        ApplyRune(player, action.Rune, action.Use);
        return null;
    }
}
