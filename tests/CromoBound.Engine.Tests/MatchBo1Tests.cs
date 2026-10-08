using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Json;
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

    [Fact]
    public void Actions_with_missing_lists_are_rejected_and_the_match_stays_playable()
    {
        var match = NewMatch();
        match.Accept(match.Decision<ChoosePlayOrderDecision>().Player, new ChoosePlayOrder(true));
        var before = match.Snapshot();
        string[] bodies =
        [
            """{"type":"SubmitSideboard","swaps":null}""",
            """{"type":"SubmitSideboard","swaps":[null]}""",
            """{"type":"SubmitSideboard","swaps":[{"out":null,"in":null}]}""",
        ];

        foreach (var body in bodies)
        {
            var action = CromoJson.Deserialize<PlayerAction>(body)!;
            var result = match.Submit(P1, action);
            Assert.Equal(RejectionCode.UnexpectedAction, result.Rejection?.Code);
        }

        Assert.Equal(before, match.Snapshot());
        Assert.Equal(new[] { P1, P2 }, match.Decision<SideboardDecision>().Players);
        match.Accept(P1, new SubmitSideboard());
        match.Accept(P2, new SubmitSideboard());
        Assert.IsType<MulliganDecision>(match.Pending);
    }

    [Fact]
    public void A_seat_that_is_not_in_the_match_cannot_concede_act_or_ask_for_undo()
    {
        var match = NewMatch().ToPlay();
        var ghost = new PlayerId(5);
        var before = match.Snapshot();
        var log = match.ToRecord().Log.Count;

        foreach (PlayerAction action in new PlayerAction[] { new Concede(), new ManualAdjustXp(P1, 1), new RequestUndo(), new AnswerUndo(true) })
            Assert.Equal(RejectionCode.NotYourDecision, match.Submit(ghost, action).Rejection?.Code);

        Assert.Equal(before, match.Snapshot());
        Assert.Equal(log, match.ToRecord().Log.Count);
        Assert.IsType<PriorityDecision>(match.Pending);
    }

    [Fact]
    public void Every_game_starts_by_revealing_both_legends()
    {
        var match = NewMatch();

        var revealed = Assert.Single(match.Events.OfType<LegendsRevealed>());

        Assert.Equal(new[] { "p-jinx-legend", "p-jinx-legend" }, revealed.Printings);
        Assert.True(revealed.Sequence > match.Events.OfType<GameStarted>().Single().Sequence);
        Assert.Null(revealed.VisibleTo);
    }

    [Fact]
    public void The_sideboard_decision_lists_the_viewers_own_deck_options_only()
    {
        var p1Deck = TestDecks.Jinx("bf-a", "bf-b", "bf-c") with
        {
            Sideboard =
            [
                new DeckEntry { Printing = "p-filler-14", Count = 3 },
                new DeckEntry { Printing = "p-jinx-alt", Count = 1 },
                new DeckEntry { Printing = "p-vi-champ", Count = 1 },
            ],
        };
        var match = Match.Create(TestDecks.Setup(MatchFormat.Bo1) with { Player1Deck = p1Deck }, EngineTestDb.Create()).Match!;
        match.Accept(match.Decision<ChoosePlayOrderDecision>().Player, new ChoosePlayOrder(true));

        var mine = Assert.IsType<SideboardDecision>(match.ViewFor(P1).Decision);
        var theirs = Assert.IsType<SideboardDecision>(match.ViewFor(P2).Decision);

        var choice = Assert.Single(mine.Choices);
        Assert.Equal(P1, choice.Player);
        Assert.Equal("p-jinx-champ", choice.Champion);
        Assert.Equal(new[] { "p-jinx-alt", "p-jinx-champ" }, choice.ChampionCandidates);
        Assert.Equal(p1Deck.Sideboard.Select(e => (e.Printing, e.Count)), choice.Sideboard.Select(e => (e.Printing, e.Count)));
        Assert.Equal(p1Deck.Main.Count, choice.Main.Count);
        var other = Assert.Single(theirs.Choices);
        Assert.Equal(P2, other.Player);
        Assert.Equal(new[] { "p-jinx-champ" }, other.ChampionCandidates);
    }
}
