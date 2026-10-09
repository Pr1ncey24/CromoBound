namespace CromoBound.Server.Hubs;

/// <summary>What the server pushes to a player's connections (spec §6.4). Public: SignalR builds the typed proxy at run time.</summary>
public interface IGameClient
{
    Task ChallengeReceived(ChallengeNotice challenge);

    Task ChallengeClosed(ChallengeClosedNotice closed);

    Task MatchStarted(MatchStartedNotice started);

    /// <summary>The receiving player's own view, never the other seat's.</summary>
    Task View(MatchViewNotice view);

    Task MatchEnded(MatchEndedNotice ended);
}
