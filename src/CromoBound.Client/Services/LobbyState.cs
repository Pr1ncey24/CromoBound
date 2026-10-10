using CromoBound.Contracts;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;

namespace CromoBound.Client.Services;

/// <summary>Everything the pages show about the lobby and the player's match, folded from <c>GetLobby</c>, <c>GetMatch</c> and the hub's
/// notices (spec §6). It also turns notices into the short notifications the layout pops up (spec §6.3).</summary>
public sealed class LobbyState(SessionState session) : IGameClient
{
    private List<PlayerPresence> _players = [];
    private List<ChallengeInfo> _received = [];
    private MatchEndedNotice? _abandoned;

    public bool Loaded { get; private set; }
    public IReadOnlyList<PlayerPresence> Players => _players;
    public int Online => _players.Count(p => p.Online);

    /// <summary>Challenges others sent the player, by challenger name.</summary>
    public IReadOnlyList<ChallengeInfo> Received => _received;

    /// <summary>The player's own open challenge; the server allows one.</summary>
    public ChallengeInfo? Sent { get; private set; }

    public Guid? MatchId { get; private set; }
    public string? Opponent { get; private set; }
    public PlayerId? Seat { get; private set; }

    /// <summary>The player's latest view of <see cref="MatchId"/>. (The name <c>View</c> is taken by the notice method.)</summary>
    public PlayerView? CurrentView { get; private set; }

    /// <summary>The last match of the player's that ended while the app was open.</summary>
    public MatchEndedNotice? Ended { get; private set; }

    public bool Maintenance { get; private set; }
    public HubState Connection { get; private set; } = HubState.Connecting;
    public bool IsConnected => Connection == HubState.Connected;

    public event Action? Changed;

    /// <summary>A player came, went or started or ended a match, or maintenance switched: what the maintenance page counts.</summary>
    public event Action? PlayersChanged;

    public event Action<string>? Notice;
    public event Action<Guid>? MatchBegun;

    private string Me => session.Me?.UserName ?? "";

    public void Load(LobbyReply lobby, MatchReply? match)
    {
        _players = Sorted(lobby.Players);
        _received = [.. lobby.Challenges.Where(c => !IsMine(c)).OrderBy(c => c.From, StringComparer.OrdinalIgnoreCase)];
        Sent = lobby.Challenges.FirstOrDefault(IsMine);
        if (lobby.MatchId != MatchId) (Opponent, Seat, CurrentView) = (null, null, null);
        MatchId = lobby.MatchId;
        if (match is { MatchId: { } id } && id == MatchId)
        {
            Opponent = match.Opponent;
            CurrentView = match.View;
            Seat = match.View?.Viewer ?? Seat;
        }
        if (lobby.Ended is { } ended) _abandoned = ended;
        Maintenance = lobby.Maintenance;
        Loaded = true;
        PlayersChanged?.Invoke();
        Changed?.Invoke();
    }

    public void SetConnection(HubState state)
    {
        Connection = state;
        Changed?.Invoke();
    }

    /// <summary>The server doesn't tell the challenger's own tabs about a new challenge, so the tab that sent it records it.</summary>
    public void ChallengeSent(ChallengeInfo challenge)
    {
        Sent = challenge;
        Changed?.Invoke();
    }

    /// <summary>A challenge the server says no longer exists.</summary>
    public void ChallengeGone(Guid challengeId)
    {
        if (Sent?.ChallengeId == challengeId) Sent = null;
        _received.RemoveAll(c => c.ChallengeId == challengeId);
        Changed?.Invoke();
    }

    /// <summary>The notice of a match abandoned at startup, once.</summary>
    public MatchEndedNotice? TakeAbandoned()
    {
        var abandoned = _abandoned;
        _abandoned = null;
        return abandoned;
    }

    public Task ChallengeReceived(ChallengeNotice challenge)
    {
        if (_received.Any(c => c.ChallengeId == challenge.ChallengeId)) return Task.CompletedTask;
        _received = [.. _received.Append(new ChallengeInfo(challenge.ChallengeId, challenge.From, Me, challenge.Format))
            .OrderBy(c => c.From, StringComparer.OrdinalIgnoreCase)];
        Notice?.Invoke($"{challenge.From} challenges you to {Formats.InSentence(challenge.Format)}.");
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task ChallengeClosed(ChallengeClosedNotice closed)
    {
        string? notice;
        if (Sent is { } sent && sent.ChallengeId == closed.ChallengeId)
        {
            Sent = null;
            notice = closed.Reason switch
            {
                ChallengeEnd.Declined => $"{sent.To} declined your challenge.",
                ChallengeEnd.Withdrawn => $"Your challenge to {sent.To} was withdrawn.",
                _ => null,
            };
        }
        else if (_received.Find(c => c.ChallengeId == closed.ChallengeId) is { } received)
        {
            _received.Remove(received);
            notice = closed.Reason switch
            {
                ChallengeEnd.Cancelled => $"{received.From} cancelled their challenge.",
                ChallengeEnd.Withdrawn => $"{received.From}'s challenge was withdrawn.",
                _ => null,
            };
        }
        else
        {
            return Task.CompletedTask;
        }
        if (notice is not null) Notice?.Invoke(notice);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task MatchStarted(MatchStartedNotice started)
    {
        (MatchId, Opponent, Seat, CurrentView, Ended) = (started.MatchId, started.Opponent, started.Seat, null, null);
        MatchBegun?.Invoke(started.MatchId);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task View(MatchViewNotice view)
    {
        if (view.MatchId != MatchId) return Task.CompletedTask;
        CurrentView = view.View;
        Seat = view.View.Viewer;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task MatchEnded(MatchEndedNotice ended)
    {
        if (ended.MatchId == MatchId) MatchId = null;
        Ended = ended;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task PlayerChanged(PlayerPresence player)
    {
        _players = Sorted(_players.Where(p => !SameName(p.UserName, player.UserName)).Append(player));
        PlayersChanged?.Invoke();
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task PlayerLeft(PlayerLeftNotice left)
    {
        _players.RemoveAll(p => SameName(p.UserName, left.UserName));
        PlayersChanged?.Invoke();
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task MaintenanceChanged(MaintenanceNotice maintenance)
    {
        Maintenance = maintenance.On;
        PlayersChanged?.Invoke();
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    private bool IsMine(ChallengeInfo challenge) => SameName(challenge.From, Me);

    private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static List<PlayerPresence> Sorted(IEnumerable<PlayerPresence> players) =>
        [.. players.OrderBy(p => p.UserName, StringComparer.OrdinalIgnoreCase)];
}
