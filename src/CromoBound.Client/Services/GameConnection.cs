using System.Text.Json;
using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Client.Services;

/// <summary>The real hub connection, over the app's own origin so the session cookie goes along, in the hub's wire JSON.</summary>
public sealed class GameConnection : IGameHub, IRetryPolicy, IAsyncDisposable
{
    public const string Unexpected = "Something went wrong.";

    private readonly HubConnection _connection;
    private readonly ForeverRetryPolicy _retry = new();
    private readonly List<IGameClient> _listeners = [];
    private readonly CancellationTokenSource _stop = new();

    public GameConnection(NavigationManager nav) : this(nav.ToAbsoluteUri("hub"), _ => { })
    {
    }

    internal GameConnection(Uri hub, Action<HttpConnectionOptions> configure)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(hub, configure)
            .WithAutomaticReconnect(this)
            .AddJsonProtocol(json => json.PayloadSerializerOptions = WireJson.Options)
            .Build();
        // Not awaited: stopping the connection waits for the reconnect, which waits for this handler.
        _connection.Reconnecting += async error =>
        {
            await SetState(HubState.Reconnecting);
            _ = DoubtSessionAsync();
        };
        _connection.Reconnected += _ => ConnectedAsync();
        _connection.Closed += _ => ClosedAsync();
        On<ChallengeNotice>(nameof(IGameClient.ChallengeReceived), (l, n) => l.ChallengeReceived(n));
        On<ChallengeClosedNotice>(nameof(IGameClient.ChallengeClosed), (l, n) => l.ChallengeClosed(n));
        On<MatchStartedNotice>(nameof(IGameClient.MatchStarted), (l, n) => l.MatchStarted(n));
        On<MatchViewNotice>(nameof(IGameClient.View), (l, n) => l.View(n));
        On<MatchEndedNotice>(nameof(IGameClient.MatchEnded), (l, n) => l.MatchEnded(n));
        On<PlayerPresence>(nameof(IGameClient.PlayerChanged), (l, n) => l.PlayerChanged(n));
        On<PlayerLeftNotice>(nameof(IGameClient.PlayerLeft), (l, n) => l.PlayerLeft(n));
        On<MaintenanceNotice>(nameof(IGameClient.MaintenanceChanged), (l, n) => l.MaintenanceChanged(n));
    }

    public HubState State { get; private set; } = HubState.Connecting;

    public event Action? StateChanged;
    public event Func<Task>? Connected;
    public event Func<Task>? Closed;
    public event Func<Task>? SessionInDoubt;

    public void Listen(IGameClient listener) => _listeners.Add(listener);

    public async Task StartAsync()
    {
        for (var attempt = 0L; !_stop.IsCancellationRequested; attempt++)
        {
            try
            {
                await _connection.StartAsync(_stop.Token);
                await ConnectedAsync();
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !_stop.IsCancellationRequested)
            {
                var wasReconnecting = State == HubState.Reconnecting;
                await SetState(HubState.Reconnecting);
                if (!wasReconnecting || IsUnauthorized(ex)) await DoubtSessionAsync();
                var delay = _retry.NextRetryDelay(new RetryContext { PreviousRetryCount = attempt, RetryReason = ex }) ?? TimeSpan.FromSeconds(30);
                try
                {
                    await Task.Delay(delay, _stop.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    public async Task StopAsync()
    {
        await _stop.CancelAsync();
        await _connection.StopAsync();
    }

    /// <summary>The automatic reconnect's own retries: the delays are <see cref="ForeverRetryPolicy"/>'s, and a retry that failed with
    /// a 401 asks for a session check, since reconnecting would go on for as long as the tab is open.</summary>
    TimeSpan? IRetryPolicy.NextRetryDelay(RetryContext retryContext)
    {
        if (IsUnauthorized(retryContext.RetryReason)) _ = DoubtSessionAsync();
        return _retry.NextRetryDelay(retryContext);
    }

    public Task<LobbyReply?> GetLobbyAsync() => QueryAsync<LobbyReply>("GetLobby");

    public Task<MatchReply?> GetMatchAsync() => QueryAsync<MatchReply>("GetMatch");

    public Task<HubReply> ChallengeAsync(string opponent, MatchFormat format, Deck deck) => ReplyAsync("Challenge", opponent, format, deck);

    public Task<HubReply> AcceptAsync(Guid challengeId, Deck deck) => ReplyAsync("AcceptChallenge", challengeId, deck);

    public Task<HubReply> DeclineAsync(Guid challengeId) => ReplyAsync("DeclineChallenge", challengeId);

    public Task<HubReply> CancelAsync(Guid challengeId) => ReplyAsync("CancelChallenge", challengeId);

    public async Task<SubmitReply> SubmitAsync(Guid matchId, PlayerAction action)
    {
        try
        {
            var payload = JsonSerializer.SerializeToElement<PlayerAction>(action, WireJson.Options);
            return await _connection.InvokeAsync<SubmitReply>("Submit", matchId, payload);
        }
        catch (Exception)
        {
            return new SubmitReply(false, null, Unexpected);
        }
    }

    private void On<T>(string method, Func<IGameClient, T, Task> deliver) =>
        _connection.On<T>(method, notice => Task.WhenAll(_listeners.Select(l => deliver(l, notice))));

    private async Task<T?> QueryAsync<T>(string method) where T : class
    {
        try
        {
            return await _connection.InvokeAsync<T>(method);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<HubReply> ReplyAsync(string method, params object?[] args)
    {
        try
        {
            return await _connection.InvokeCoreAsync<HubReply>(method, args);
        }
        catch (Exception)
        {
            return HubReply.Fail(Unexpected);
        }
    }

    private static bool IsUnauthorized(Exception? ex) => ex is HttpRequestException { StatusCode: System.Net.HttpStatusCode.Unauthorized };

    private async Task DoubtSessionAsync()
    {
        try
        {
            if (SessionInDoubt is { } doubt) await doubt();
        }
        catch (Exception)
        {
            // The check is best effort: the next attempt asks again.
        }
    }

    private async Task ConnectedAsync()
    {
        await SetState(HubState.Connected);
        if (Connected is { } connected) await connected();
    }

    private async Task ClosedAsync()
    {
        await SetState(HubState.Reconnecting);
        if (!_stop.IsCancellationRequested && Closed is { } closed) await closed();
    }

    private Task SetState(HubState state)
    {
        State = state;
        StateChanged?.Invoke();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        await _connection.DisposeAsync();
        _stop.Dispose();
    }
}
