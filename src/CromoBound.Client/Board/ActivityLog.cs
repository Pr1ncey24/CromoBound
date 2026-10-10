using CromoBound.Engine;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;

namespace CromoBound.Client.Board;

/// <summary>The view's events as short sentences, oldest first (spec §7). Events that only restate what the board shows (phases,
/// statuses, resources, chain bookkeeping, forced choices and targets, control and attachments, looks, reveals and shuffles) are left
/// out; an event type without a sentence shows its type name.</summary>
public static class ActivityLog
{
    public const int MaxLines = 200;

    public static IReadOnlyList<string> Lines(PlayerView view, CardBook book, string me, string opponent)
    {
        var cards = CardIds(view);
        var lines = new List<string>();
        foreach (var e in view.Log)
            if (Sentence(e, view, book, cards, opponent) is { } line) lines.Add(line);
        return lines.Count > MaxLines ? lines.GetRange(lines.Count - MaxLines, MaxLines) : lines;
    }

    private static string? Sentence(GameEvent e, PlayerView view, CardBook book, Dictionary<ObjectId, string> cards, string opponent)
    {
        string Who(PlayerId player) => player == view.Viewer ? "You" : opponent;
        string Verb(PlayerId player, string you, string they) => player == view.Viewer ? you : they;
        string Card(ObjectId? id) => id is { } known && cards.TryGetValue(known, out var cardId) ? book.NameOf(cardId) : CardBook.Unknown;
        string Lane(int index) => view.Battlefields.FirstOrDefault(b => b.Index == index) is { } lane ? book.NameOf(lane.Card.CardId) : $"battlefield {index + 1}";
        static string Plural(int count, string one, string many) => count == 1 ? one : many;

        return e switch
        {
            GameStarted started => $"Game {started.GameNumber} begins.",
            BattlefieldsChosen chosen => $"Battlefields: {string.Join(", ", chosen.Printings.Select(p => book.PrintingOf(p) is { } printing ? book.NameOf(printing.CardId) : CardBook.Unknown))}.",
            D20Rolled rolled => $"{Who(rolled.Player)} rolled {rolled.Value}.",
            PlayOrderChosen order => $"{Who(order.First)} {Verb(order.First, "go", "goes")} first.",
            MulliganTaken taken => taken.Count == 0
                ? $"{Who(taken.Player)} kept the opening hand."
                : $"{Who(taken.Player)} set aside {taken.Count} {Plural(taken.Count, "card", "cards")} and drew {taken.Count}.",
            SideboardChanged changed => $"{Who(changed.Player)} swapped {changed.Swaps} {Plural(changed.Swaps, "card", "cards")} with the sideboard.",
            TurnStarted turn => turn.Player == view.Viewer ? $"Turn {turn.Number}: your turn." : $"Turn {turn.Number}: {opponent}'s turn.",
            CardPlayed played => $"{Who(played.Controller)} played {book.NameOf(played.CardId)}.",
            AbilityActivated activated => $"{Who(activated.Controller)} used {Card(activated.Source)}'s ability.",
            CardMoved moved => Moved(moved, Who, Card, Lane, book),
            DamageDealt damage => $"{Card(damage.Unit)} took {damage.Amount} damage.",
            UnitDied died => $"{book.NameOf(died.CardId)} died.",
            PointsChanged points => $"{Who(points.Player)} now {Verb(points.Player, "have", "has")} {points.Points} {Plural(points.Points, "point", "points")}.",
            XpChanged xp => $"{Who(xp.Player)} now {Verb(xp.Player, "have", "has")} {xp.Xp} XP.",
            BattlefieldScored scored => scored.GainedPoint
                ? $"{Who(scored.Player)} {(scored.Kind == ScoreKind.Conquer ? "conquered" : "held")} {Lane(scored.Battlefield)}: +1 point."
                : null,
            ShowdownStarted showdown => $"A showdown started at {Lane(showdown.Battlefield)}.",
            CombatStarted combat => $"{Who(combat.Attacker)} attacked at {Lane(combat.Battlefield)}.",
            PlayCancelled => "A play was cancelled.",
            BurnedOut burned => $"{Who(burned.Player)} burned out.",
            TokenCreated token => $"A {book.NameOf(token.CardId)} token was made.",
            UndoRequested undo => $"{Who(undo.Player)} asked to undo the last action.",
            ManualActionTaken manual => $"{Who(manual.Player)} did something by hand ({manual.Kind}).",
            GameEnded ended => ended.Winner is { } winner ? $"{Who(winner)} won the game{Reason(ended.Reason)}." : "The game ended without a winner.",
            MatchEnded ended => $"{Who(ended.Winner)} won the match.",
            PhaseStarted or StatusChanged or ResourcesAdded or CostAdjusted or ChainItemAdded or ChainItemResolved or ChoiceMade or TargetsChosen
                or TriggerAdded or LegendsRevealed or GameRecorded or UnitsHealed or UnitHealed or MightModified or PoolAdjusted or ControlChanged
                or ControlGained or ShowdownEnded or CombatEnded or DeckShuffled or CardsLookedAt or CardRevealed or ChainItemCountered
                or PlayerChosen or Predicted or Attached or Detached => null,
            _ => e.GetType().Name,
        };
    }

    /// <summary>Draws, trashings, banishments and standard moves are told; other moves restate the board.</summary>
    private static string? Moved(CardMoved moved, Func<PlayerId, string> who, Func<ObjectId?, string> card, Func<int, string> lane, CardBook book)
    {
        var name = moved.CardId is { } cardId ? book.NameOf(cardId) : card(moved.From);
        return (moved.FromPlace.Kind, moved.ToPlace.Kind) switch
        {
            (PlaceKind.MainDeck, PlaceKind.Hand) when moved.ToPlace.Player is { } player => $"{who(player)} drew a card.",
            (_, PlaceKind.Trash) => $"{name} went to the trash.",
            (_, PlaceKind.Banishment) => $"{name} was banished.",
            (PlaceKind.Base or PlaceKind.Battlefield, PlaceKind.Battlefield) when moved.ToPlace.Index is { } index => $"{name} moved to {lane(index)}.",
            (PlaceKind.Battlefield, PlaceKind.Base) => $"{name} went back to its base.",
            _ => null,
        };
    }

    private static string Reason(GameEndReason reason) => reason switch
    {
        GameEndReason.Concede => " by concession",
        GameEndReason.BurnOut => " by burn out",
        _ => "",
    };

    /// <summary>The card behind each object id the viewer may know: the cards on view, and the ids the log names.</summary>
    private static Dictionary<ObjectId, string> CardIds(PlayerView view)
    {
        var cards = new Dictionary<ObjectId, string>();
        foreach (var card in view.Players.SelectMany(p => p.Legend.Concat(p.ChampionZone).Concat(p.Base).Concat(p.Hand ?? []).Concat(p.Trash).Concat(p.Banishment))
                     .Concat(view.Battlefields.SelectMany(b => b.Units.Append(b.Card))))
            cards[card.Id] = card.CardId;
        foreach (var e in view.Log)
        {
            switch (e)
            {
                case CardMoved { CardId: { } cardId } moved:
                    if (moved.From is { } from) cards.TryAdd(from, cardId);
                    if (moved.To is { } to) cards.TryAdd(to, cardId);
                    break;
                case CardPlayed played:
                    cards.TryAdd(played.Card, played.CardId);
                    break;
                case UnitDied died:
                    cards.TryAdd(died.Unit, died.CardId);
                    break;
                case TokenCreated token:
                    cards.TryAdd(token.Token, token.CardId);
                    break;
            }
        }
        return cards;
    }
}
