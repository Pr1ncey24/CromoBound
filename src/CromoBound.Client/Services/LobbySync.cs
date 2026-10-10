namespace CromoBound.Client.Services;

/// <summary>Feeds <see cref="LobbyState"/> from the hub (spec §5.2): notices as they come, the whole lobby again after every connect and
/// reconnect, and, when the server closes the connection, a session check before connecting again.</summary>
public sealed class LobbySync(IGameHub hub, LobbyState state, IServerApi api, SessionState session)
{
    private bool _started;

    public Task StartAsync()
    {
        if (_started) return Task.CompletedTask;
        _started = true;
        hub.Listen(state);
        hub.StateChanged += () => state.SetConnection(hub.State);
        hub.Connected += ReloadAsync;
        hub.Closed += ClosedAsync;
        return hub.StartAsync();
    }

    /// <summary>A failed call leaves the state as it is; the next reconnect tries again.</summary>
    public async Task ReloadAsync()
    {
        if (await hub.GetLobbyAsync() is not { } lobby) return;
        var match = lobby.MatchId is null ? null : await hub.GetMatchAsync();
        state.Load(lobby, match);
    }

    private async Task ClosedAsync()
    {
        await api.MeAsync();
        if (!session.IsEnded) await hub.StartAsync();
    }
}
