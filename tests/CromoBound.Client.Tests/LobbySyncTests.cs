using CromoBound.Client.Services;
using CromoBound.Client.Tests.Fakes;
using CromoBound.Contracts;

namespace CromoBound.Client.Tests;

public class LobbySyncTests
{
    private static (LobbySync Sync, FakeGameHub Hub, LobbyState State, FakeServerApi Api, SessionState Session) Sync()
    {
        var session = new SessionState();
        session.SignedIn(new MeResponse("marco", false));
        var hub = new FakeGameHub();
        var state = new LobbyState(session);
        var api = new FakeServerApi(session);
        return (new LobbySync(hub, state, api, session), hub, state, api, session);
    }

    [Fact]
    public async Task Every_connect_reloads_the_lobby()
    {
        var (sync, hub, state, _, _) = Sync();
        await sync.StartAsync();
        hub.Lobby = new LobbyReply([new("giulia", true, false)], [], null, null, false);

        await hub.SetStateAsync(HubState.Connected);
        await hub.SetStateAsync(HubState.Reconnecting);
        Assert.Equal(HubState.Reconnecting, state.Connection);
        await hub.SetStateAsync(HubState.Connected);

        Assert.Equal(1, hub.Starts);
        Assert.Equal(new[] { "GetLobby", "GetLobby" }, hub.Calls);
        Assert.Equal("giulia", Assert.Single(state.Players).UserName);
        Assert.Equal(HubState.Connected, state.Connection);
    }

    [Fact]
    public async Task A_running_match_is_fetched_with_the_lobby()
    {
        var (sync, hub, state, _, _) = Sync();
        var match = Guid.NewGuid();
        hub.Lobby = new LobbyReply([], [], match, null, false);
        hub.Match = new MatchReply(match, null, "giulia");
        await sync.StartAsync();

        await hub.SetStateAsync(HubState.Connected);

        Assert.Equal(new[] { "GetLobby", "GetMatch" }, hub.Calls);
        Assert.Equal((match, "giulia"), (state.MatchId, state.Opponent));
    }

    [Fact]
    public async Task Notices_reach_the_lobby_state()
    {
        var (sync, hub, state, _, _) = Sync();
        await sync.StartAsync();
        await hub.SetStateAsync(HubState.Connected);

        await hub.Push(c => c.MaintenanceChanged(new MaintenanceNotice(true)));

        Assert.True(state.Maintenance);
    }

    [Fact]
    public async Task A_closed_connection_checks_the_session_then_starts_again()
    {
        var (sync, hub, _, api, _) = Sync();
        await sync.StartAsync();

        await hub.CloseAsync();

        Assert.Equal(1, api.MeCalls);
        Assert.Equal(2, hub.Starts);
    }

    [Fact]
    public async Task A_closed_connection_with_the_session_over_stays_closed()
    {
        var (sync, hub, _, api, session) = Sync();
        await sync.StartAsync();
        api.SessionOver = true;

        await hub.CloseAsync();

        Assert.True(session.IsEnded);
        Assert.Equal(1, hub.Starts);
    }

    [Fact]
    public async Task Moving_to_reconnecting_with_the_session_over_ends_it_and_stops_the_hub()
    {
        var (sync, hub, _, api, session) = Sync();
        await sync.StartAsync();
        await hub.SetStateAsync(HubState.Connected);
        api.SessionOver = true;

        await hub.SetStateAsync(HubState.Reconnecting);

        Assert.Equal(1, api.MeCalls);
        Assert.True(session.IsEnded);
        Assert.Equal(1, hub.Stops);
    }

    [Fact]
    public async Task Moving_to_reconnecting_with_a_live_session_pings_once_and_goes_on()
    {
        var (sync, hub, _, api, session) = Sync();
        await sync.StartAsync();
        await hub.SetStateAsync(HubState.Connected);

        await hub.SetStateAsync(HubState.Reconnecting);

        Assert.Equal(1, api.MeCalls);
        Assert.False(session.IsEnded);
        Assert.Equal(0, hub.Stops);
    }

    [Fact]
    public async Task An_attempt_refused_with_401_checks_the_session_again()
    {
        var (sync, hub, _, api, session) = Sync();
        await sync.StartAsync();
        await hub.SetStateAsync(HubState.Reconnecting);
        api.SessionOver = true;

        await hub.DoubtSessionAsync();

        Assert.Equal(2, api.MeCalls);
        Assert.True(session.IsEnded);
        Assert.Equal(1, hub.Stops);
    }
}
