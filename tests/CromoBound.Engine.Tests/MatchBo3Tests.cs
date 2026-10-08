using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class MatchBo3Tests
{
    private static Match NewBo3(MatchSetup? setup = null) =>
        Match.Create(setup ?? TestDecks.Setup(MatchFormat.Bo3), EngineTestDb.Create()).Match!;

    private static PlayerId Other(PlayerId player) => player == P1 ? P2 : P1;

    /// <summary>Plays the current game's pre-game, then the given player concedes it.</summary>
    private static void LoseGame(Match match, PlayerId loser)
    {
        match.PickFirstBattlefields().ToPlay();
        match.Accept(loser, new Concede());
    }

    [Fact]
    public void Both_players_pick_an_unused_battlefield_in_any_order()
    {
        var match = NewBo3();
        var pick = match.Decision<PickBattlefieldDecision>();
        Assert.Equal(new[] { P1, P2 }, pick.Players);
        Assert.Equal(new[] { "p-bf-a", "p-bf-b", "p-bf-c" }, pick.Choices.Single(c => c.Player == P1).Printings);

        Assert.Equal(RejectionCode.UnexpectedAction, match.Submit(P1, new PickBattlefield("p-bf-d")).Rejection!.Code);
        match.Accept(P2, new PickBattlefield("p-bf-e"));
        Assert.Equal(new[] { P1 }, match.Decision<PickBattlefieldDecision>().Players);
        match.Accept(P1, new PickBattlefield("p-bf-a"));

        Assert.Equal(new[] { "p-bf-a", "p-bf-e" }, Assert.Single(match.Events.OfType<BattlefieldsChosen>()).Printings);
        Assert.IsType<ChoosePlayOrderDecision>(match.Pending);
    }

    [Fact]
    public void Game_one_of_a_bo3_has_no_sideboarding()
    {
        var match = NewBo3().PickFirstBattlefields();
        var order = match.Decision<ChoosePlayOrderDecision>();

        match.Accept(order.Player, new ChoosePlayOrder(true));

        Assert.IsType<MulliganDecision>(match.Pending);
    }

    [Fact]
    public void Conceding_records_the_game_removes_its_battlefields_and_the_loser_chooses_next()
    {
        var match = NewBo3();
        match.PickFirstBattlefields().ToPlay();
        var loser = match.Game!.State.Turn.TurnPlayer;

        match.Accept(loser, new Concede());

        var recorded = Assert.Single(match.Events.OfType<GameRecorded>());
        Assert.Equal((Other(loser), GameEndReason.Concede), (recorded.Winner!.Value, recorded.Reason));
        Assert.Equal(2, match.GameNumber);
        Assert.Equal(new[] { "p-bf-b", "p-bf-c" }, match.Decision<PickBattlefieldDecision>().Choices.Single(c => c.Player == P1).Printings);
        match.PickFirstBattlefields();
        Assert.Equal(loser, match.Decision<ChoosePlayOrderDecision>().Player);
        match.Accept(loser, new ChoosePlayOrder(false));
        Assert.IsType<SideboardDecision>(match.Pending);
    }

    [Fact]
    public void Illegal_sideboard_is_rejected_and_can_be_resubmitted()
    {
        var match = NewBo3();
        LoseGame(match, P2);
        match.PickFirstBattlefields();
        match.Accept(P2, new ChoosePlayOrder(true));

        var missing = match.Submit(P1, new SubmitSideboard { Swaps = [new SideboardSwap("p-filler-14", "p-filler-1")] });
        var illegal = match.Submit(P1, new SubmitSideboard { Champion = "p-filler-2" });

        Assert.Equal(RejectionCode.InvalidSideboard, missing.Rejection!.Code);
        Assert.Equal(RejectionCode.InvalidSideboard, illegal.Rejection!.Code);
        Assert.Contains("champion", illegal.Rejection.Message, StringComparison.OrdinalIgnoreCase);
        match.Accept(P1, new SubmitSideboard { Swaps = [new SideboardSwap("p-filler-1", "p-filler-14")] });
        Assert.Equal(new[] { P2 }, match.Decision<SideboardDecision>().Players);
    }

    [Fact]
    public void The_champion_can_be_switched_while_sideboarding()
    {
        var p1Deck = TestDecks.Jinx("bf-a", "bf-b", "bf-c") with
        {
            Sideboard = [new DeckEntry { Printing = "p-filler-14", Count = 3 }, new DeckEntry { Printing = "p-jinx-alt", Count = 1 }],
        };
        var match = NewBo3(TestDecks.Setup(MatchFormat.Bo3) with { Player1Deck = p1Deck });
        LoseGame(match, P1);
        match.PickFirstBattlefields();
        match.Accept(P1, new ChoosePlayOrder(true));

        match.Accept(P1, new SubmitSideboard { Champion = "p-jinx-alt" });

        Assert.Equal("p-jinx-alt", match.CurrentDecks[0].Champion);
        Assert.Contains(match.CurrentDecks[0].Sideboard, e => e.Printing == "p-jinx-champ");
        Assert.Equal("p-jinx-champ", p1Deck.Champion);
    }

    [Fact]
    public void Two_wins_end_the_match_and_a_single_battlefield_left_is_picked_automatically()
    {
        var match = NewBo3();
        LoseGame(match, P2);
        LoseGame(match, P1);

        Assert.Equal(3, match.GameNumber);
        Assert.IsNotType<PickBattlefieldDecision>(match.Pending);
        Assert.Equal(new[] { "p-bf-c", "p-bf-f" }, match.Events.OfType<BattlefieldsChosen>().Last().Printings);

        match.ToPlay();
        match.Accept(P2, new Concede());

        Assert.Equal(MatchStage.Over, match.Stage);
        Assert.Equal(P1, match.Result.Winner);
        Assert.Equal(new[] { 2, 1 }, match.Result.GameWins);
        Assert.Single(match.Events.OfType<MatchEnded>());
        Assert.Equal(RejectionCode.MatchOver, match.Submit(P1, new Concede()).Rejection!.Code);
    }

    [Fact]
    public void A_game_ended_before_setup_removes_no_battlefields()
    {
        var match = NewBo3();
        match.Accept(P1, new PickBattlefield("p-bf-a"));
        match.Accept(P2, new PickBattlefield("p-bf-d"));
        match.Accept(P1, new Concede());

        Assert.Equal(2, match.GameNumber);
        var pick = match.Decision<PickBattlefieldDecision>();
        Assert.Equal(new[] { "p-bf-a", "p-bf-b", "p-bf-c" }, pick.Choices.Single(c => c.Player == P1).Printings);
        Assert.Equal(new[] { "p-bf-d", "p-bf-e", "p-bf-f" }, pick.Choices.Single(c => c.Player == P2).Printings);
    }

    [Fact]
    public void A_concede_during_the_pick_also_removes_none()
    {
        var match = NewBo3();
        match.Accept(P2, new Concede());

        Assert.Equal(3, match.Decision<PickBattlefieldDecision>().Choices.Single(c => c.Player == P1).Printings.Count);
    }

    [Fact]
    public void Each_game_reveals_the_legends()
    {
        var match = NewBo3();
        LoseGame(match, P2);

        var reveals = match.Events.OfType<LegendsRevealed>().ToList();

        Assert.Equal(2, reveals.Count);
        Assert.All(reveals, r => Assert.Equal(new[] { "p-jinx-legend", "p-jinx-legend" }, r.Printings));
    }
}
