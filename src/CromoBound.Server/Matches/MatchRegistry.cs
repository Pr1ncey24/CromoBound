using System.Collections.Concurrent;
using CromoBound.Engine.Matches;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Matches;

/// <summary>The running matches (spec §6.6), by id and by player. A player is in at most one.</summary>
internal sealed class MatchRegistry(IHubContext<GameHub, IGameClient> hub)
{
    private readonly ConcurrentDictionary<Guid, MatchHost> _hosts = new();
    private readonly ConcurrentDictionary<int, MatchHost> _byUser = new();

    public int Count => _hosts.Count;

    public bool IsPlaying(int userId) => _byUser.ContainsKey(userId);

    public MatchHost? Find(Guid matchId) => _hosts.GetValueOrDefault(matchId);

    /// <summary>Holds a match that was just created.</summary>
    public MatchHost Open(Guid id, Match match, IReadOnlyList<MatchSeat> seats)
    {
        var host = new MatchHost(id, match, seats, hub);
        _hosts[id] = host;
        foreach (var seat in seats) _byUser[seat.UserId] = host;
        return host;
    }

    /// <summary>The user's running match and their view of it, or nothing.</summary>
    public async Task<MatchReply> CurrentAsync(int userId) =>
        _byUser.TryGetValue(userId, out var host) && host.SeatOf(userId) is { } seat
            ? new MatchReply(host.Id, await host.ViewAsync(seat), null)
            : MatchReply.None;
}
