namespace CromoBound.Contracts;

/// <summary>What the server pushes to a player's connections (spec §6.4). Public: SignalR builds the typed proxy at run time.</summary>
public interface IGameClient
{
    Task ChallengeReceived(ChallengeNotice challenge);

    Task ChallengeClosed(ChallengeClosedNotice closed);

    Task MatchStarted(MatchStartedNotice started);

    /// <summary>The receiving player's own view, never the other seat's.</summary>
    Task View(MatchViewNotice view);

    Task MatchEnded(MatchEndedNotice ended);

    /// <summary>Another player came, went, or started or ended a match, or an admin created or re-enabled them.</summary>
    Task PlayerChanged(PlayerPresence player);

    Task PlayerLeft(PlayerLeftNotice left);

    Task MaintenanceChanged(MaintenanceNotice maintenance);
}
