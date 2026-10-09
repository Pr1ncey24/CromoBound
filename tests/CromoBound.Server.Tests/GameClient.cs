using System.Text.Json;
using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>A player's hub connection over the test server's WebSockets, signed in with a session cookie. It keeps every notice the
/// server pushes, in arrival order.</summary>
internal sealed class GameClient : IAsyncDisposable
{
    private readonly List<object> _received = [];
    private readonly SemaphoreSlim _arrived = new(0);
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private GameClient(HubConnection connection)
    {
        Connection = connection;
        Record<ChallengeNotice>(nameof(IGameClient.ChallengeReceived));
        Record<ChallengeClosedNotice>(nameof(IGameClient.ChallengeClosed));
        Record<MatchStartedNotice>(nameof(IGameClient.MatchStarted));
        Record<MatchViewNotice>(nameof(IGameClient.View));
        Record<MatchEndedNotice>(nameof(IGameClient.MatchEnded));
        Record<PlayerPresence>(nameof(IGameClient.PlayerChanged));
        Record<PlayerLeftNotice>(nameof(IGameClient.PlayerLeft));
        Record<MaintenanceNotice>(nameof(IGameClient.MaintenanceChanged));
        connection.Closed += _ =>
        {
            _closed.TrySetResult();
            return Task.CompletedTask;
        };
    }

    public HubConnection Connection { get; }

    /// <summary>Completes when the server closes the connection.</summary>
    public Task Closed => _closed.Task;

    /// <summary>Adds a user with <see cref="ServerFactory.PlayerPassword"/> and connects as them.</summary>
    public static async Task<GameClient> NewPlayerAsync(ServerFactory factory, string userName)
    {
        await factory.AddUserAsync(userName);
        return await ConnectAsync(factory, userName);
    }

    public static async Task<GameClient> ConnectAsync(ServerFactory factory, string userName, string password = ServerFactory.PlayerPassword) =>
        await ConnectWithCookieAsync(factory, await factory.SessionCookieAsync(userName, password));

    /// <summary>Connects with the given cookie header value; throws when the server refuses the connection.</summary>
    public static async Task<GameClient> ConnectWithCookieAsync(ServerFactory factory, string cookie)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl("https://localhost/hub", options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, cancel) =>
                {
                    var socket = factory.Server.CreateWebSocketClient();
                    socket.ConfigureRequest = request => request.Headers.Cookie = cookie;
                    return await socket.ConnectAsync(context.Uri, cancel);
                };
            })
            .AddJsonProtocol(json => json.PayloadSerializerOptions = WireJson.Options)
            .Build();
        var client = new GameClient(connection);
        await connection.StartAsync();
        // StartAsync returns before the server has put the connection in its user's group, so a notice sent right away could miss it. The
        // server runs a connection's calls only after it has joined the group, so a reply proves it. This call changes nothing: it asks
        // to cancel a challenge that doesn't exist (GetMatch would use up a one-time abandoned notice). A connection refused at the
        // upgrade still throws from StartAsync.
        await connection.InvokeAsync<HubReply>("CancelChallenge", Guid.Empty);
        return client;
    }

    public Task<HubReply> ChallengeAsync(string opponent, Deck? deck, MatchFormat format = MatchFormat.Bo1) =>
        Connection.InvokeAsync<HubReply>("Challenge", opponent, format, deck);

    public Task<HubReply> DeclineAsync(Guid challengeId) => Connection.InvokeAsync<HubReply>("DeclineChallenge", challengeId);

    public Task<HubReply> CancelAsync(Guid challengeId) => Connection.InvokeAsync<HubReply>("CancelChallenge", challengeId);

    public Task<HubReply> AcceptAsync(Guid challengeId, Deck? deck) => Connection.InvokeAsync<HubReply>("AcceptChallenge", challengeId, deck);

    public Task<MatchReply> GetMatchAsync() => Connection.InvokeAsync<MatchReply>("GetMatch");

    public Task<LobbyReply> GetLobbyAsync() => Connection.InvokeAsync<LobbyReply>("GetLobby");

    /// <summary>Sends the action as a <see cref="PlayerAction"/>, so its type name travels with it.</summary>
    public Task<SubmitReply> SubmitAsync(Guid matchId, PlayerAction action) =>
        Connection.InvokeAsync<SubmitReply>("Submit", matchId, JsonSerializer.SerializeToElement(action, WireJson.Options));

    /// <summary>Sends the value as it is: a payload without a "type" name doesn't say which action it is.</summary>
    public Task<SubmitReply> SubmitRawAsync(Guid matchId, object? action) => Connection.InvokeAsync<SubmitReply>("Submit", matchId, action);

    /// <summary>Every notice of this type received so far.</summary>
    public IReadOnlyList<T> All<T>()
    {
        lock (_received) return [.. _received.OfType<T>()];
    }

    /// <summary>The first notice of this type (matching <paramref name="match"/>, if given), waiting up to ten seconds for it.</summary>
    public Task<T> WaitForAsync<T>(Func<T, bool>? match = null) => WaitForAsync(match ?? (_ => true), after: 0);

    /// <summary>The first notice of this type matching <paramref name="match"/> among those after the first <paramref name="after"/>
    /// of its type, waiting up to ten seconds for it.</summary>
    public async Task<T> WaitForAsync<T>(Func<T, bool> match, int after)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            lock (_received)
                foreach (var notice in _received.OfType<T>().Skip(after))
                    if (match(notice)) return notice;
            await _arrived.WaitAsync(timeout.Token);
        }
    }

    private void Record<T>(string method) => Connection.On<T>(method, notice =>
    {
        lock (_received) _received.Add(notice!);
        _arrived.Release();
    });

    public async ValueTask DisposeAsync() => await Connection.DisposeAsync();
}
