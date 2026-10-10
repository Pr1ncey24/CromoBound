using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using Microsoft.AspNetCore.Http.Connections;

namespace CromoBound.Server.Tests;

/// <summary>The client's own <see cref="GameConnection"/> against the real hub, over the test server's WebSockets.</summary>
public class ClientConnectionTests
{
    [Fact]
    public async Task Two_client_connections_challenge_play_and_end_a_match()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("alice");
        await factory.AddUserAsync("bob");
        var (alice, aliceNotices) = await ConnectAsync(factory, "alice");
        var (bob, bobNotices) = await ConnectAsync(factory, "bob");
        await using var aliceConnection = alice;
        await using var bobConnection = bob;

        var challenge = await alice.ChallengeAsync("bob", MatchFormat.Bo1, Decks.First);
        Assert.Null(challenge.Error);
        var accepted = await bob.AcceptAsync(challenge.Id!.Value, Decks.Second);
        Assert.Null(accepted.Error);
        var matchId = accepted.Id!.Value;

        Assert.Equal(matchId, (await aliceNotices.WaitForAsync<MatchStartedNotice>()).MatchId);
        Assert.Equal(matchId, (await bobNotices.WaitForAsync<MatchStartedNotice>()).MatchId);
        Assert.Equal(matchId, (await alice.GetLobbyAsync())!.MatchId);
        Assert.Equal("bob", (await alice.GetMatchAsync())!.Opponent);

        Assert.True((await alice.SubmitAsync(matchId, new Concede())).Accepted);

        var ended = await bobNotices.WaitForAsync<MatchEndedNotice>();
        Assert.Equal((matchId, "bob"), (ended.MatchId, ended.Winner));
    }

    /// <summary>Starts a connection signed in as the user, as the browser's would be by its cookie, with a listener keeping the notices.</summary>
    internal static async Task<(GameConnection Connection, Notices Notices)> ConnectAsync(ServerFactory factory, string userName)
    {
        var cookie = await factory.SessionCookieAsync(userName, ServerFactory.PlayerPassword);
        var connection = new GameConnection(new Uri("https://localhost/hub"), options =>
        {
            options.Transports = HttpTransportType.WebSockets;
            options.SkipNegotiation = true;
            options.WebSocketFactory = async (context, cancel) =>
            {
                var socket = factory.Server.CreateWebSocketClient();
                socket.ConfigureRequest = request => request.Headers.Cookie = cookie;
                return await socket.ConnectAsync(context.Uri, cancel);
            };
        });
        var notices = new Notices();
        connection.Listen(notices);
        await connection.StartAsync();
        // StartAsync returns before the server has put the connection in its user's group, and the server runs a connection's calls only
        // after it has: this call, which cancels a challenge that doesn't exist, proves the connection is in.
        await connection.CancelAsync(Guid.Empty);
        return (connection, notices);
    }

    /// <summary>The notices a connection received, waited for with a bound.</summary>
    internal sealed class Notices : IGameClient
    {
        private readonly List<object> _received = [];
        private readonly SemaphoreSlim _arrived = new(0);

        public async Task<T> WaitForAsync<T>()
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            while (true)
            {
                lock (_received)
                    if (_received.OfType<T>().FirstOrDefault() is { } notice) return notice;
                await _arrived.WaitAsync(timeout.Token);
            }
        }

        public Task ChallengeReceived(ChallengeNotice challenge) => Record(challenge);

        public Task ChallengeClosed(ChallengeClosedNotice closed) => Record(closed);

        public Task MatchStarted(MatchStartedNotice started) => Record(started);

        public Task View(MatchViewNotice view) => Record(view);

        public Task MatchEnded(MatchEndedNotice ended) => Record(ended);

        public Task PlayerChanged(PlayerPresence player) => Record(player);

        public Task PlayerLeft(PlayerLeftNotice left) => Record(left);

        public Task MaintenanceChanged(MaintenanceNotice maintenance) => Record(maintenance);

        private Task Record(object notice)
        {
            lock (_received) _received.Add(notice);
            _arrived.Release();
            return Task.CompletedTask;
        }
    }
}
