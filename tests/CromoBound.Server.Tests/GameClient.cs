using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using CromoBound.Server.Hubs;
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
            .AddJsonProtocol(json => json.PayloadSerializerOptions = ServerJson.Options)
            .Build();
        var client = new GameClient(connection);
        await connection.StartAsync();
        return client;
    }

    public Task<HubReply> ChallengeAsync(string opponent, Deck? deck, MatchFormat format = MatchFormat.Bo1) =>
        Connection.InvokeAsync<HubReply>("Challenge", opponent, format, deck);

    public Task<HubReply> DeclineAsync(Guid challengeId) => Connection.InvokeAsync<HubReply>("DeclineChallenge", challengeId);

    public Task<HubReply> CancelAsync(Guid challengeId) => Connection.InvokeAsync<HubReply>("CancelChallenge", challengeId);

    /// <summary>Every notice of this type received so far.</summary>
    public IReadOnlyList<T> All<T>()
    {
        lock (_received) return [.. _received.OfType<T>()];
    }

    /// <summary>The first notice of this type (matching <paramref name="match"/>, if given), waiting up to ten seconds for it.</summary>
    public async Task<T> WaitForAsync<T>(Func<T, bool>? match = null)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            lock (_received)
                foreach (var notice in _received.OfType<T>())
                    if (match?.Invoke(notice) ?? true) return notice;
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
