using System.Globalization;
using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using CromoBound.Server.Accounts;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Hubs;

/// <summary>The one hub (spec §6), at /hub behind the player policy. Every connection joins its user's group, so all of a user's tabs
/// get their notices. Methods answer with replies; anything unexpected is SignalR's generic error. A user's first connection and last
/// disconnection, and the end of a match, are announced to the other players.</summary>
internal sealed class GameHub(Lobby lobby, MatchRegistry matches, LiveConnections connections, Presence presence, Maintenance maintenance,
    CromoDbContext db) : Hub<IGameClient>
{
    public static string UserGroup(int userId) => "user-" + userId.ToString(CultureInfo.InvariantCulture);

    private MatchSeat Me => new(Sessions.UserId(Context.User!)!.Value, Context.User!.Identity!.Name!);

    /// <summary>The connection is tracked before its session is checked again, so an account change made in between still closes it.
    /// Only a connection that passes the check is announced.</summary>
    public override async Task OnConnectedAsync()
    {
        var me = Me;
        var first = connections.Opened(Context.ConnectionId, me.UserId, Context.Abort);
        if (!await Sessions.IsCurrentAsync(Context.User!, db))
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(me.UserId));
        if (first) await presence.AnnounceAsync(me.UserId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (connections.Closed(Context.ConnectionId, out var userId)) await presence.AnnounceAsync(userId);
    }

    /// <summary>Everything the lobby shows, on connect and on reconnect (spec §6.1). The abandoned notice is handed out once.</summary>
    public async Task<LobbyReply> GetLobby()
    {
        var me = Me;
        var others = await db.Users.AsNoTracking().Where(u => !u.Disabled && u.Id != me.UserId)
            .OrderBy(u => u.NormalizedUserName).Select(u => new { u.Id, u.UserName }).ToListAsync();
        return new LobbyReply(
            [.. others.Select(u => presence.Of(u.Id, u.UserName))],
            await lobby.ChallengesOfAsync(me.UserId),
            matches.MatchOf(me.UserId),
            matches.TakeAbandoned(me.UserId),
            maintenance.On);
    }

    public Task<HubReply> Challenge(string? opponent, MatchFormat format, Deck? deck) => lobby.ChallengeAsync(Me, opponent, format, deck);

    public Task<HubReply> AcceptChallenge(Guid challengeId, Deck? deck) => lobby.AcceptAsync(Me, challengeId, deck);

    public Task<HubReply> DeclineChallenge(Guid challengeId) => lobby.DeclineAsync(Me, challengeId);

    public Task<HubReply> CancelChallenge(Guid challengeId) => lobby.CancelAsync(Me, challengeId);

    /// <summary>The caller's running match and view.</summary>
    public Task<MatchReply> GetMatch() => matches.CurrentAsync(Me.UserId);

    /// <summary>Any action, manual ones, undo and concede included, in the caller's own seat (spec §6.3). An action that ends the match
    /// announces both players as free again.</summary>
    public async Task<SubmitReply> Submit(Guid matchId, PlayerAction? action)
    {
        var host = matches.Find(matchId);
        var reply = await matches.SubmitAsync(Me.UserId, matchId, action);
        if (reply.Accepted && host is not null && matches.Find(matchId) is null)
            foreach (var seat in host.Seats) await presence.AnnounceAsync(seat.UserId);
        return reply;
    }
}
