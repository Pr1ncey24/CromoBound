using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Services;

public enum HubState { Connecting, Connected, Reconnecting }

/// <summary>The tab's one hub connection (spec §5.2). Notices go to every listener; calls never throw: a failed call answers with the
/// generic error, or null for the two queries.</summary>
public interface IGameHub
{
    HubState State { get; }

    event Action? StateChanged;

    /// <summary>After every connect and reconnect.</summary>
    event Func<Task>? Connected;

    /// <summary>The connection is closed for good: the server closed it, or reconnecting stopped.</summary>
    event Func<Task>? Closed;

    /// <summary>The connection may have lost its session: it moved into reconnecting, or an attempt was answered with a 401. The
    /// listener checks the session.</summary>
    event Func<Task>? SessionInDoubt;

    void Listen(IGameClient listener);

    /// <summary>Connects, trying again with the reconnect delays until it does.</summary>
    Task StartAsync();

    /// <summary>Gives up connecting and reconnecting for good; <see cref="Closed"/> doesn't fire.</summary>
    Task StopAsync();

    Task<LobbyReply?> GetLobbyAsync();
    Task<MatchReply?> GetMatchAsync();
    Task<HubReply> ChallengeAsync(string opponent, MatchFormat format, Deck deck);
    Task<HubReply> AcceptAsync(Guid challengeId, Deck deck);
    Task<HubReply> DeclineAsync(Guid challengeId);
    Task<HubReply> CancelAsync(Guid challengeId);
    Task<SubmitReply> SubmitAsync(Guid matchId, PlayerAction action);
}
