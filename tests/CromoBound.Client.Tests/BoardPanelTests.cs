using CromoBound.Client.Board;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Tests;

public class BoardPanelTests
{
    [Fact]
    public void Resolve_by_hand_shows_the_card_and_its_text()
    {
        var model = new TestBoard().Model(new ResolveManuallyDecision(TestBoard.Me, 3, "unit-a", "The first time I move each turn, choose a player."));

        Assert.Equal(new ResolvePanel("Blade Twirler", "p-unit-a", "The first time I move each turn, choose a player."), model.Panel);
        Assert.False(model.Button.Enabled);
    }

    [Theory]
    [InlineData(TurnPoint.StartOfBeginning, "Start of your Beginning Phase")]
    [InlineData(TurnPoint.StartOfMain, "Start of your Main Phase")]
    [InlineData(TurnPoint.EndOfTurn, "End of your turn")]
    public void A_turn_point_lists_the_cards_to_apply_by_hand(TurnPoint point, string title)
    {
        var board = new TestBoard();
        var unit = board.Add(board.MyBase, "unit-b", might: 1);

        var panel = Assert.IsType<TurnPointPanel>(board.Model(new TurnPointDecision(TestBoard.Me, point, [unit.Id])).Panel);

        Assert.Equal(title, panel.Title);
        Assert.Equal("Daring Poro", Assert.Single(panel.Cards).Name);
    }

    [Fact]
    public void An_undo_request_names_who_asks_and_their_last_action()
    {
        var board = new TestBoard();
        var spell = board.Card("spell-a", TestBoard.Them);
        board.Log.Add(new CardPlayed(spell.Id, "spell-a", TestBoard.Them));
        board.Log.Add(new UndoRequested(TestBoard.Them));

        var model = board.Model(new ConfirmUndoDecision(TestBoard.Me, TestBoard.Them));

        Assert.Equal(new UndoPanel("giulia", "giulia played Angle Shot."), model.Panel);
    }

    [Fact]
    public void While_giulia_answers_an_undo_the_request_waits()
    {
        var board = new TestBoard();
        board.Log.Add(new UndoRequested(TestBoard.Me));

        var model = board.Model(deciding: [TestBoard.Them], kind: "ConfirmUndo");

        Assert.True(model.UndoWaiting);
        Assert.False(model.CanRequestUndo);
        Assert.Null(model.Panel);
    }

    [Fact]
    public void Undo_can_be_asked_during_a_game_once_something_happened()
    {
        var board = new TestBoard();
        Assert.False(board.Model(TestBoard.Priority()).CanRequestUndo);

        board.Log.Add(new TurnStarted(TestBoard.Me, 1));
        Assert.True(board.Model(TestBoard.Priority()).CanRequestUndo);

        board.Stage = MatchStage.Mulligan;
        Assert.False(board.Model().CanRequestUndo);
    }

    [Fact]
    public void A_choice_of_a_later_update_says_what_it_is()
    {
        var model = new TestBoard().Model(new ChooseShowdownDecision(TestBoard.Me, [0], false));

        Assert.Equal(new LaterPanel("ChooseShowdown", "choose where the showdown happens"), model.Panel);
        Assert.False(model.Button.Enabled);
    }

    [Fact]
    public void The_battlefield_pick_offers_the_unused_battlefields()
    {
        var board = new TestBoard { Stage = MatchStage.PickBattlefields };

        var model = board.Model(new PickBattlefieldDecision([TestBoard.Me, TestBoard.Them],
            [new BattlefieldChoice(TestBoard.Me, ["p-bf-b", "p-bf-c"])]));

        var panel = Assert.IsType<PickBattlefieldPanel>(model.Panel);
        Assert.Equal((2, 3, false, "giulia"), (panel.GameNumber, panel.Games, panel.Waiting, panel.Opponent));
        Assert.Equal(new[] { new PrintingOption("p-bf-b", "Altar to Unity"), new PrintingOption("p-bf-c", "Dusk Rose Lab") }, panel.Options);
    }

    [Fact]
    public void While_giulia_picks_the_board_waits_for_her()
    {
        var board = new TestBoard { Stage = MatchStage.PickBattlefields };
        var card = board.Add(board.Hand, "unit-b");

        var model = board.Model(deciding: [TestBoard.Them], kind: "PickBattlefield");

        var panel = Assert.IsType<PickBattlefieldPanel>(model.Panel);
        Assert.Equal((true, "giulia"), (panel.Waiting, panel.Opponent));
        Assert.Empty(panel.Options);
        Assert.IsType<NoStep>(model.Click(card.Id));
        Assert.False(model.Button.Enabled);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void The_play_order_is_asked_or_waited_for(bool waiting)
    {
        var board = new TestBoard { Stage = MatchStage.PlayOrder, GameNumber = 1 };

        var model = waiting ? board.Model(deciding: [TestBoard.Them], kind: "ChoosePlayOrder") : board.Model(new ChoosePlayOrderDecision(TestBoard.Me));

        Assert.Equal(new PlayOrderPanel(1, waiting, "giulia"), model.Panel);
    }

    [Fact]
    public void Sideboarding_lists_the_main_deck_and_the_sideboard_by_name()
    {
        var board = new TestBoard { Stage = MatchStage.Sideboarding };
        var choice = new SideboardChoice(TestBoard.Me,
            [new DeckEntry { Printing = "p-unit-a", Count = 3 }, new DeckEntry { Printing = "p-unit-b", Count = 2 }],
            [new DeckEntry { Printing = "p-spell-a", Count = 2 }], "p-jinx-champ", []);

        var panel = Assert.IsType<SideboardPanel>(board.Model(new SideboardDecision([TestBoard.Me], [choice])).Panel);

        Assert.Equal(new[] { new DeckRow("p-unit-a", "Blade Twirler", 3), new DeckRow("p-unit-b", "Daring Poro", 2) }, panel.Main);
        Assert.Equal(new[] { new DeckRow("p-spell-a", "Angle Shot", 2) }, panel.Sideboard);
        Assert.False(panel.Waiting);
    }

    [Fact]
    public void The_mulligan_shows_the_opening_hand()
    {
        var board = new TestBoard { Stage = MatchStage.Mulligan };
        var a = board.Add(board.Hand, "unit-a");
        var b = board.Add(board.Hand, "spell-a");

        var panel = Assert.IsType<MulliganPanel>(board.Model(new MulliganDecision(TestBoard.Me, [a.Id, b.Id])).Panel);

        Assert.Equal(new[] { "Blade Twirler", "Angle Shot" }, panel.Hand.Select(c => c.Name));
        Assert.False(panel.Waiting);
    }

    [Fact]
    public void The_log_is_filled()
    {
        var board = new TestBoard();
        board.Log.Add(new TurnStarted(TestBoard.Me, 5));

        Assert.Equal(new[] { "Turn 5: your turn." }, board.Model().Log);
    }
}
