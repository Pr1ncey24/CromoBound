using CromoBound.Client.Board;
using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Server.Cards;

namespace CromoBound.Server.Tests;

/// <summary>A first turn played end to end: the real hub, the client's GameConnection, and the board model choosing every action the
/// way a player's clicks would (spec 10).</summary>
public class BoardGameTests
{
    [Fact]
    public async Task A_first_turn_is_played_through_the_board_model()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("alice");
        await factory.AddUserAsync("bob");
        var (alice, aliceNotices) = await ClientConnectionTests.ConnectAsync(factory, "alice");
        var (bob, _) = await ClientConnectionTests.ConnectAsync(factory, "bob");
        await using var aliceConnection = alice;
        await using var bobConnection = bob;
        var challenge = await alice.ChallengeAsync("bob", MatchFormat.Bo1, Decks.First);
        var matchId = (await bob.AcceptAsync(challenge.Id!.Value, Decks.Second)).Id!.Value;
        await aliceNotices.WaitForAsync<MatchStartedNotice>();
        var book = new CardBook(CardEndpoints.CatalogOf(ServerFactory.TestCards));
        var players = new[] { (Connection: alice, Me: "alice", Opponent: "bob"), (Connection: bob, Me: "bob", Opponent: "alice") };
        var played = false;
        PlayerView? last = null;

        for (var step = 0; step < 80 && last?.Turn is not { Number: >= 2 }; step++)
        {
            var acted = false;
            foreach (var player in players)
            {
                var view = (await player.Connection.GetMatchAsync())!.View!;
                last = view;
                if (view.Turn is { Number: >= 2 }) break;
                if (view.Decision is null) continue;
                var model = BoardModel.From(view, book, player.Me, player.Opponent, Idle.Instance);
                var action = Choose(model, view, book, ref played);
                var reply = await player.Connection.SubmitAsync(matchId, action);
                Assert.True(reply.Accepted, $"{action.GetType().Name} was refused: {reply.Rejection?.Message ?? reply.Error}");
                acted = true;
                break;
            }
            Assert.True(acted || last?.Turn is { Number: >= 2 }, "Nobody had a decision.");
        }

        Assert.True(played, "No card was played on the first turn.");
        Assert.Equal(2, last?.Turn?.Number);
        var firstPlayer = last!.Players.Single(p => p.Player != last.Turn!.TurnPlayer);
        Assert.Contains(firstPlayer.Base, c => c.CardId.StartsWith("filler-", StringComparison.Ordinal));
    }

    /// <summary>What a player would click: send the sideboard as it is, go first, keep the hand, play one 1-energy card on the first turn and pay it as suggested,
    /// otherwise the big button (pass or end the turn).</summary>
    private static PlayerAction Choose(BoardModel model, PlayerView view, CardBook book, ref bool played)
    {
        switch (model.Panel)
        {
            case SideboardPanel:
                return new SubmitSideboard();
            case PlayOrderPanel:
                return new ChoosePlayOrder(true);
            case MulliganPanel:
                return new Mulligan();
            case PlayOptionsPanel options:
                return new ChoosePlayOptions(options.Locations.FirstOrDefault()?.Place, false);
            case ResolvePanel:
                return new ResolveDone();
            case TurnPointPanel:
                return new ContinueTurn();
        }
        if (!played && view.Turn?.TurnPlayer == view.Viewer && model.Button.Label != "PAY")
        {
            foreach (var card in model.Me.Hand)
                if (book.Card(card.CardId)?.Energy == 1 && model.Click(card.Id) is SendStep { Action: PlayCard play })
                {
                    played = true;
                    return play;
                }
        }
        return Assert.IsType<SendStep>(model.Button.Step).Action;
    }
}
