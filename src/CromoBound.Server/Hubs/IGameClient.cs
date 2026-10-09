namespace CromoBound.Server.Hubs;

/// <summary>What the server pushes to a player's connections (spec §6.4). Public: SignalR builds the typed proxy at run time.</summary>
public interface IGameClient
{
    Task ChallengeReceived(ChallengeNotice challenge);

    Task ChallengeClosed(ChallengeClosedNotice closed);
}
