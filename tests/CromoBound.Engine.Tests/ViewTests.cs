using CromoBound.Engine.Actions;
using CromoBound.Engine.Decisions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class ViewTests
{
    private static Match NewMatch(MatchFormat format = MatchFormat.Bo1, ulong seed = 7) =>
        Match.Create(TestDecks.Setup(format, seed), EngineTestDb.Create()).Match!;

    /// <summary>Object ids appear in JSON as { "value": n }.</summary>
    private static HashSet<int> ObjectIdsIn(string json) =>
        [.. System.Text.RegularExpressions.Regex.Matches(json, @"\{\s*""value"":\s*(\d+)\s*\}").Select(m => int.Parse(m.Groups[1].Value))];

    [Fact]
    public void Opponent_view_and_log_never_contain_hidden_cards()
    {
        var match = NewMatch().ToPlay();
        var state = match.Game!.State;
        var p2Hand = state.At(Place.Hand(P2)).ToList();
        match.Accept(P2, new ManualMoveCard(p2Hand[0], Place.MainDeck(P2)));
        match.Accept(P2, new ManualMoveCard(p2Hand[1], Place.Trash(P2)));
        match.Accept(P2, new ManualMoveCard(state.At(Place.Trash(P2))[0], Place.MainDeck(P2), DeckPosition.Bottom));
        match.Accept(state.Turn.TurnPlayer, new EndTurn());

        var json = CromoJson.Serialize(match.ViewFor(P1));

        var hidden = state.At(Place.Hand(P2))
            .Concat(new[] { P1, P2 }.SelectMany(p => state.At(Place.MainDeck(p)).Concat(state.At(Place.RuneDeck(p)))))
            .Select(id => id.Value)
            .ToHashSet();
        Assert.Empty(ObjectIdsIn(json).Intersect(hidden));
    }

    [Fact]
    public void A_player_sees_their_own_hand_and_only_counts_of_the_opponents()
    {
        var match = NewMatch().ToPlay();
        var state = match.Game!.State;

        var view = match.ViewFor(P1);

        Assert.Equal(state.At(Place.Hand(P1)).Count, view.Players[0].Hand!.Count);
        Assert.Null(view.Players[1].Hand);
        Assert.Equal(state.At(Place.Hand(P2)).Count, view.Players[1].HandCount);
        Assert.Equal(state.At(Place.MainDeck(P2)).Count, view.Players[1].MainDeckCount);
        Assert.NotNull(view.Players[0].Sideboard);
        Assert.Null(view.Players[1].Sideboard);
        Assert.Equal(3, view.Players[1].SideboardCount);
    }

    [Fact]
    public void A_facedown_card_shows_only_to_its_controller()
    {
        var match = NewMatch().ToPlay();
        var state = match.Game!.State;
        var player = state.Turn.TurnPlayer;
        var other = player == P1 ? P2 : P1;
        state.Battlefields[0].Controller = player;
        match.Accept(player, new ManualCreateToken("token-recruit", Place.Battlefield(0), player));
        match.Accept(player, new ManualMoveCard(state.At(Place.Hand(player))[0], Place.Facedown(0)));

        var mine = match.ViewFor(player).Battlefields[0];
        var theirs = match.ViewFor(other).Battlefields[0];

        Assert.NotNull(mine.Facedown);
        Assert.True(theirs.HasFacedown);
        Assert.Null(theirs.Facedown);
    }

    [Fact]
    public void Only_deciders_see_a_decisions_options()
    {
        var bo3 = NewMatch(MatchFormat.Bo3);

        var p1View = bo3.ViewFor(P1);
        var pick = Assert.IsType<PickBattlefieldDecision>(p1View.Decision);
        Assert.Equal(P1, Assert.Single(pick.Choices).Player);
        Assert.Equal("PickBattlefield", p1View.DecisionKind);

        var bo1 = NewMatch().ToPlay();
        var decider = bo1.Pending!.Players[0];
        var watcher = decider == P1 ? P2 : P1;
        Assert.IsType<PriorityDecision>(bo1.ViewFor(decider).Decision);
        Assert.Null(bo1.ViewFor(watcher).Decision);
        Assert.Equal(new[] { decider }, bo1.ViewFor(watcher).Deciding);
    }

    [Fact]
    public void Scripted_players_finish_a_bo3_and_the_saved_match_replays_identically()
    {
        var match = NewMatch(MatchFormat.Bo3, seed: 11);
        var bots = new[] { new Bot(), new Bot() };
        for (var i = 0; i < 20000 && match.Stage != MatchStage.Over; i++)
        {
            var player = match.Pending!.Players[0];
            match.Accept(player, bots[player.Index].Choose(match));
        }

        Assert.Equal(MatchStage.Over, match.Stage);
        Assert.Contains(2, match.Result.GameWins);
        var loaded = Match.Load(match.ToRecord(), EngineTestDb.Create());
        Assert.Equal(CromoJson.Serialize(match.ViewFor(P1)), CromoJson.Serialize(loaded.ViewFor(P1)));
    }
}
