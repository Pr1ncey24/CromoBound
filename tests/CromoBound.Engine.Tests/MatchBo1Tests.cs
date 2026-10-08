using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class MatchBo1Tests
{
    private static Match NewMatch(ulong seed = 7) => Match.Create(TestDecks.Setup(MatchFormat.Bo1, seed), EngineTestDb.Create()).Match!;

    [Fact]
    public void An_illegal_deck_creates_no_match_and_reports_why()
    {
        var setup = TestDecks.Setup(MatchFormat.Bo1) with { Player2Deck = TestDecks.Jinx("bf-d", "bf-e") };

        var result = Match.Create(setup, EngineTestDb.Create());

        Assert.Null(result.Match);
        Assert.True(result.Reports[0].IsLegal);
        Assert.Contains(result.Reports[1].Issues, i => i.Code == DeckIssueCode.BattlefieldCount);
    }

    [Fact]
    public void Battlefields_are_random_and_the_roll_off_winner_chooses_play_order()
    {
        var match = NewMatch();

        var chosen = Assert.Single(match.Events.OfType<BattlefieldsChosen>()).Printings;
        Assert.Contains(chosen[0], new[] { "p-bf-a", "p-bf-b", "p-bf-c" });
        Assert.Contains(chosen[1], new[] { "p-bf-d", "p-bf-e", "p-bf-f" });
        var rolls = match.Events.OfType<D20Rolled>().ToList();
        Assert.All(rolls.SkipLast(2).Chunk(2), tie => Assert.Equal(tie[0].Value, tie[1].Value));
        var final = rolls.TakeLast(2).ToList();
        Assert.NotEqual(final[0].Value, final[1].Value);
        Assert.Equal(final.MaxBy(r => r.Value)!.Player, match.Decision<ChoosePlayOrderDecision>().Player);
    }

    [Fact]
    public void Bo1_goes_through_sideboarding_and_mulligan_to_the_first_turn()
    {
        var match = NewMatch();
        var first = match.Decision<ChoosePlayOrderDecision>().Player;
        match.Accept(first, new ChoosePlayOrder(true));

        Assert.Equal(new[] { P1, P2 }, match.Decision<SideboardDecision>().Players);
        match.Accept(P2, new SubmitSideboard());
        match.Accept(P1, new SubmitSideboard { Swaps = [new SideboardSwap("p-filler-1", "p-filler-14")] });
        Assert.Equal(2, match.CurrentDecks[0].Main.Single(e => e.Printing == "p-filler-1").Count);
        Assert.Equal(1, match.CurrentDecks[0].Main.Single(e => e.Printing == "p-filler-14").Count);

        var mulligan = match.Decision<MulliganDecision>();
        Assert.Equal(first, mulligan.Player);
        Assert.Equal(4, mulligan.Hand.Count);
        var game = match.Game!;
        var asideCards = mulligan.Hand.Take(2).Select(id => game.State[id].CardId).Order().ToList();
        match.Accept(first, new Mulligan { SetAside = [mulligan.Hand[0], mulligan.Hand[1]] });
        var deck = game.State.At(Place.MainDeck(first));
        Assert.Equal(asideCards, deck.TakeLast(2).Select(id => game.State[id].CardId).Order().ToList());
        match.Accept(match.Decision<MulliganDecision>().Player, new Mulligan());

        Assert.Equal(MatchStage.Playing, match.Stage);
        Assert.Equal(first, game.State.Turn.TurnPlayer);
        Assert.Equal(5, game.State.At(Place.Hand(first)).Count);
        Assert.IsType<PriorityDecision>(match.Pending);
    }

    [Fact]
    public void A_mulligan_of_more_than_two_cards_is_rejected()
    {
        var match = NewMatch().ToMulligan();
        var mulligan = match.Decision<MulliganDecision>();

        var result = match.Submit(mulligan.Player, new Mulligan { SetAside = [.. mulligan.Hand.Take(3)] });

        Assert.Equal(RejectionCode.UnexpectedAction, result.Rejection!.Code);
        Assert.IsType<MulliganDecision>(match.Pending);
    }

    [Fact]
    public void Game_actions_go_to_the_game()
    {
        var match = NewMatch().ToPlay();
        var player = match.Decision<PriorityDecision>().Player;

        match.Accept(player, new EndTurn());

        Assert.Equal(2, match.Game!.State.Turn.Number);
        Assert.Equal(1, match.Events.OfType<TurnStarted>().Count(t => t.Number == 2));
    }

    [Fact]
    public void The_same_seed_gives_the_same_match_events()
    {
        static string Describe(Match m) => string.Join("|", m.ToPlay().Events.Select(e => $"{e.Sequence}:{e.GetType().Name}"));

        Assert.Equal(Describe(NewMatch(seed: 9)), Describe(NewMatch(seed: 9)));
    }
}
