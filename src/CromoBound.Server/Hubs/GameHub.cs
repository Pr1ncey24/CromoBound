using System.Globalization;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using CromoBound.Server.Accounts;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Hubs;

/// <summary>The one hub (spec §6), at /hub behind the player policy. Every connection joins its user's group, so all of a user's tabs
/// get their notices. Methods answer with replies; anything unexpected is SignalR's generic error.</summary>
internal sealed class GameHub(Lobby lobby, LiveConnections connections, CromoDbContext db) : Hub<IGameClient>
{
    public static string UserGroup(int userId) => "user-" + userId.ToString(CultureInfo.InvariantCulture);

    private MatchSeat Me => new(Sessions.UserId(Context.User!)!.Value, Context.User!.Identity!.Name!);

    /// <summary>The connection is tracked before its session is checked again, so an account change made in between still closes it.</summary>
    public override async Task OnConnectedAsync()
    {
        var me = Me;
        connections.Opened(Context.ConnectionId, me.UserId, Context.Abort);
        if (!await Sessions.IsCurrentAsync(Context.User!, db))
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(me.UserId));
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Closed(Context.ConnectionId);
        return Task.CompletedTask;
    }

    public Task<HubReply> Challenge(string? opponent, MatchFormat format, Deck? deck) => lobby.ChallengeAsync(Me, opponent, format, deck);

    public Task<HubReply> DeclineChallenge(Guid challengeId) => lobby.DeclineAsync(Me, challengeId);

    public Task<HubReply> CancelChallenge(Guid challengeId) => lobby.CancelAsync(Me, challengeId);
}
