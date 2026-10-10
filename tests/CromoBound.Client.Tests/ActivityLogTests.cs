using CromoBound.Client.Board;
using CromoBound.Engine;
using CromoBound.Engine.Events;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Client.Tests;

public class ActivityLogTests
{
    private sealed record Mystery : GameEvent;

    private static IReadOnlyList<string> Lines(TestBoard board) => ActivityLog.Lines(board.View(), board.Book, "marco", "giulia");

    [Fact]
    public void Events_read_as_short_sentences_for_you_and_for_the_opponent()
    {
        var board = new TestBoard();
        board.AddLane("bf-a");
        var unit = board.Add(board.MyBase, "unit-a", might: 2);
        board.Log.AddRange(
        [
            new GameStarted(1),
            new D20Rolled(TestBoard.Them, 17),
            new PlayOrderChosen(TestBoard.Them, TestBoard.Them),
            new MulliganTaken(TestBoard.Me, 0),
            new MulliganTaken(TestBoard.Them, 2),
            new TurnStarted(TestBoard.Them, 1),
            new TurnStarted(TestBoard.Me, 2),
            new CardPlayed(unit.Id, "unit-a", TestBoard.Me),
            new CardMoved("unit-a", unit.Id, unit.Id, Place.Base(TestBoard.Me), Place.Battlefield(0)),
            new BattlefieldScored(TestBoard.Me, 0, ScoreKind.Hold, true),
            new PointsChanged(TestBoard.Me, 6),
            new PointsChanged(TestBoard.Them, 1),
            new DamageDealt(unit.Id, 2),
            new UnitDied(unit.Id, "unit-a", TestBoard.Me),
            new UndoRequested(TestBoard.Them),
            new GameEnded(TestBoard.Them, GameEndReason.Concede),
        ]);

        Assert.Equal(new[]
        {
            "Game 1 begins.",
            "giulia rolled 17.",
            "giulia goes first.",
            "You kept the opening hand.",
            "giulia set aside 2 cards and drew 2.",
            "Turn 1: giulia's turn.",
            "Turn 2: your turn.",
            "You played Blade Twirler.",
            "Blade Twirler moved to Back-Alley Bar.",
            "You held Back-Alley Bar: +1 point.",
            "You now have 6 points.",
            "giulia now has 1 point.",
            "Blade Twirler took 2 damage.",
            "Blade Twirler died.",
            "giulia asked to undo the last action.",
            "giulia won the game by concession.",
        }, Lines(board));
    }

    [Fact]
    public void Draws_trash_and_banishment_are_told()
    {
        var board = new TestBoard();
        board.Log.AddRange(
        [
            new CardMoved(null, null, null, Place.MainDeck(TestBoard.Them), Place.Hand(TestBoard.Them)),
            new CardMoved("spell-a", new ObjectId(90), new ObjectId(91), Place.Chain, Place.Trash(TestBoard.Me)),
            new CardMoved("unit-b", new ObjectId(92), new ObjectId(93), Place.Base(TestBoard.Me), Place.Banishment(TestBoard.Me)),
        ]);

        Assert.Equal(new[] { "giulia drew a card.", "Angle Shot went to the trash.", "Daring Poro was banished." }, Lines(board));
    }

    [Fact]
    public void Events_that_restate_the_board_are_left_out_and_unknown_ones_show_their_type()
    {
        var board = new TestBoard();
        board.Log.AddRange(
        [
            new PhaseStarted(Phase.Main, TurnStep.None),
            new StatusChanged(new ObjectId(1), StatusKind.Exhausted, true),
            new ResourcesAdded(TestBoard.Me, 1, null),
            new ChainItemAdded(1, TestBoard.Me),
            new Mystery(),
        ]);

        Assert.Equal(new[] { "Mystery" }, Lines(board));
    }

    [Fact]
    public void A_death_is_told_once_but_a_card_trashed_otherwise_still_is()
    {
        var board = new TestBoard();
        var unit = board.Add(board.MyBase, "unit-a", might: 2);
        board.Log.AddRange(
        [
            new UnitDied(unit.Id, "unit-a", TestBoard.Me),
            new CardMoved("unit-a", unit.Id, new ObjectId(80), Place.Base(TestBoard.Me), Place.Trash(TestBoard.Me)),
            new CardMoved("spell-a", new ObjectId(90), new ObjectId(91), Place.Chain, Place.Trash(TestBoard.Me)),
        ]);

        Assert.Equal(new[] { "Blade Twirler died.", "Angle Shot went to the trash." }, Lines(board));
    }

    [Fact]
    public void Only_the_last_lines_are_kept()
    {
        var board = new TestBoard();
        for (var i = 1; i <= ActivityLog.MaxLines + 5; i++) board.Log.Add(new TurnStarted(TestBoard.Me, i));

        var lines = Lines(board);

        Assert.Equal(ActivityLog.MaxLines, lines.Count);
        Assert.Equal("Turn 6: your turn.", lines[0]);
    }
}
