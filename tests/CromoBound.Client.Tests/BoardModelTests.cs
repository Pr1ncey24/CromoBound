using CromoBound.Client.Board;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Effects;

namespace CromoBound.Client.Tests;

public class BoardModelTests
{
    [Fact]
    public void The_sides_show_points_xp_piles_and_the_legend_and_champion()
    {
        var board = new TestBoard();
        board.Add(board.MyTrash, "spell-a");
        board.Add(board.MyTrash, "unit-b");
        board.Add(board.MyBanished, "gear-a");

        var model = board.Model();

        Assert.Equal(("marco", true, 5, 1), (model.Me.Name, model.Me.IsMe, model.Me.Points, model.Me.Xp));
        Assert.Equal(("giulia", false, 6, 2), (model.Them.Name, model.Them.IsMe, model.Them.Points, model.Them.Xp));
        Assert.Equal(("Jinx, Loose Cannon", "Jinx, Rebel"), (model.Me.Legend?.Name, model.Me.Champion?.Name));
        Assert.Equal(("Daring Poro", 2), (model.Me.TrashTop?.Name, model.Me.TrashCount));
        Assert.Equal("Doran's Blade", Assert.Single(model.Me.Banished).Name);
        Assert.Equal((29, 7, 4), (model.Me.MainDeckCount, model.Me.RuneDeckCount, model.Them.HandCount));
        Assert.Null(model.Them.TrashTop);
    }

    [Fact]
    public void Runes_are_apart_from_the_units_and_gear_of_the_base()
    {
        var board = new TestBoard();
        board.Add(board.MyBase, "fury-rune");
        board.Add(board.MyBase, "unit-a", might: 2);
        board.Add(board.MyBase, "order-rune", exhausted: true);
        board.Add(board.MyBase, "gear-a");

        var model = board.Model();

        Assert.Equal(new[] { "Blade Twirler", "Doran's Blade" }, model.Me.Base.Select(c => c.Name));
        Assert.Equal(new[] { ("Fury Rune", false), ("Order Rune", true) }, model.Me.Runes.Select(c => (c.Name, c.Exhausted)));
    }

    [Fact]
    public void A_card_carries_its_state_its_printing_and_what_changed_from_the_printed_card()
    {
        var board = new TestBoard();
        var unit = board.Add(board.MyBase, "unit-a", damage: 1, might: 4);
        board.Add(board.MyBase, "gear-a", attachedTo: unit.Id);
        board.Add(board.MyBase, "unit-b", might: 1);

        var model = board.Model();

        var twirler = model.Me.Base.Single(c => c.Id == unit.Id);
        Assert.Equal(("p-unit-a", 1, 4, 2, true, 1), (twirler.PrintingId, twirler.Damage, twirler.Might, twirler.PrintedMight, twirler.MightChanged, twirler.Gear));
        Assert.False(model.Me.Base.Single(c => c.Name == "Daring Poro").MightChanged);
    }

    [Fact]
    public void Lanes_split_the_units_by_side_and_name_who_holds_them()
    {
        var board = new TestBoard();
        board.AddLane("bf-a", TestBoard.Me);
        board.AddLane("bf-b", TestBoard.Them);
        board.AddToLane(0, "unit-a", TestBoard.Me, might: 2);
        board.AddToLane(1, "unit-b", TestBoard.Them, might: 1);
        board.AddToLane(1, "unit-a", TestBoard.Me, exhausted: true, might: 2);

        var model = board.Model();

        Assert.Equal(new[] { "Back-Alley Bar", "Altar to Unity" }, model.Lanes.Select(l => l.Card.Name));
        Assert.Equal(((PlayerId?)TestBoard.Me, 1, 0), (model.Lanes[0].Controller, model.Lanes[0].Mine.Count, model.Lanes[0].Theirs.Count));
        Assert.Equal(("Daring Poro", "Blade Twirler", true), (model.Lanes[1].Theirs[0].Name, model.Lanes[1].Mine[0].Name, model.Lanes[1].Mine[0].Exhausted));
    }

    [Fact]
    public void The_chain_names_its_cards_and_their_controllers_newest_first()
    {
        var board = new TestBoard();
        board.Chain.Add(new ChainItemView(1, ChainItemKind.Card, TestBoard.Them, ChainItemStatus.Finalized, board.Card("spell-a", TestBoard.Them),
            null, null, null, false, []));
        board.Chain.Add(new ChainItemView(2, ChainItemKind.Ability, TestBoard.Me, ChainItemStatus.Pending, null, "unit-a", "text", null, false, []));

        var model = board.Model();

        Assert.Equal(new[] { new ChainRow(2, "Blade Twirler", "marco"), new ChainRow(1, "Angle Shot", "giulia") }, model.Chain);
    }

    [Theory]
    [InlineData(1, 0, "Best of three · game 2 · you lead 1 : 0")]
    [InlineData(0, 1, "Best of three · game 2 · giulia leads 1 : 0")]
    [InlineData(1, 1, "Best of three · game 2 · 1 : 1")]
    public void The_score_line_says_who_leads(int mine, int theirs, string line)
    {
        var board = new TestBoard { MyWins = mine, TheirWins = theirs };

        Assert.Equal(line, board.Model().ScoreLine);
    }

    [Fact]
    public void The_turn_badge_and_phase_follow_the_turn()
    {
        var board = new TestBoard();
        Assert.Equal(("YOUR TURN", 5, (Phase?)Phase.Main), (board.Model().Badge, board.Model().Turn, board.Model().Phase));

        board.TurnPlayer = TestBoard.Them;
        board.Phase = Phase.Channel;
        Assert.Equal(("GIULIA'S TURN", (Phase?)Phase.Channel), (board.Model().Badge, board.Model().Phase));
    }

    [Fact]
    public void Without_a_decision_of_mine_nothing_is_clickable_and_the_button_waits()
    {
        var board = new TestBoard();
        var card = board.Add(board.Hand, "unit-b");
        board.TurnPlayer = TestBoard.Them;

        var model = board.Model(deciding: [TestBoard.Them], kind: "Priority");

        Assert.False(model.MyDecision);
        Assert.False(model.Me.Hand.Single().Clickable);
        Assert.IsType<NoStep>(model.Click(card.Id));
        Assert.Equal(("END TURN", "Waiting for giulia", false), (model.Button.Label, model.Button.Detail, model.Button.Enabled));
    }

    [Fact]
    public void Before_the_game_has_a_turn_there_is_no_badge_or_phase()
    {
        var board = new TestBoard { Stage = MatchStage.Mulligan };

        var model = board.Model();

        Assert.Equal(("", 0, (Phase?)null), (model.Badge, model.Turn, model.Phase));
    }

    [Fact]
    public void Gear_attached_to_a_unit_in_the_base_is_only_a_count_on_that_unit()
    {
        var board = new TestBoard();
        var unit = board.Add(board.MyBase, "unit-a", might: 2);
        board.Add(board.MyBase, "gear-a", attachedTo: unit.Id);
        board.Add(board.MyBase, "gear-a");

        var model = board.Model();

        Assert.Equal(new[] { "Blade Twirler", "Doran's Blade" }, model.Me.Base.Select(c => c.Name));
        Assert.Equal(1, model.Me.Base.Single(c => c.Id == unit.Id).Gear);
    }

    [Fact]
    public void Gear_attached_to_a_unit_at_a_battlefield_is_only_a_count_on_that_unit()
    {
        var board = new TestBoard();
        board.AddLane("bf-a", TestBoard.Me);
        var mine = board.AddToLane(0, "unit-a", TestBoard.Me, might: 2);
        board.Lanes[0].Units.Add(board.Card("gear-a", TestBoard.Me, attachedTo: mine.Id));
        var theirs = board.AddToLane(0, "unit-b", TestBoard.Them, might: 1);
        board.Lanes[0].Units.Add(board.Card("gear-a", TestBoard.Them, attachedTo: theirs.Id));

        var lane = Assert.Single(board.Model().Lanes);

        Assert.Equal(("Blade Twirler", 1), (Assert.Single(lane.Mine).Name, lane.Mine[0].Gear));
        Assert.Equal(("Daring Poro", 1), (Assert.Single(lane.Theirs).Name, lane.Theirs[0].Gear));
    }
}
