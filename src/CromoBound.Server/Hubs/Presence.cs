using CromoBound.Contracts;
using CromoBound.Server.Accounts;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Hubs;

/// <summary>How players stand in the lobby (spec §6.2): online when they have an open connection, and in a match.</summary>
internal sealed class Presence(LiveConnections connections, MatchRegistry matches, IHubContext<GameHub, IGameClient> hub, IServiceScopeFactory scopes,
    ILogger<Presence> log)
{
    public PlayerPresence Of(int userId, string userName) => new(userName, connections.IsOnline(userId), matches.IsPlaying(userId));

    /// <summary>Tells every other connected player how the user stands now: PlayerChanged while the account is enabled, PlayerLeft once
    /// it is disabled. The account is read here, so a disabled user's last disconnect announces them as left again, never as offline. The
    /// user's own connections aren't told. Announcing is best-effort: a failure is logged and never reaches the caller, whose own work
    /// (a connection, a started match, a created user) has already happened. GetLobby always reads the current state, so a lost
    /// announcement heals on the next GetLobby.</summary>
    public async Task AnnounceAsync(int userId)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var user = await scope.ServiceProvider.GetRequiredService<CromoDbContext>().Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
            if (user is null) return;
            var others = hub.Clients.AllExcept(connections.ConnectionsOf(userId));
            if (user.Disabled) await others.PlayerLeft(new PlayerLeftNotice(user.UserName));
            else await others.PlayerChanged(Of(user.Id, user.UserName));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogWarning(ex, "Announcing the presence of user {UserId} failed.", userId);
        }
    }
}
