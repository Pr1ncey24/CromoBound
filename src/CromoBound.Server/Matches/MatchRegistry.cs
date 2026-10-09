using System.Collections.Concurrent;
using CromoBound.Contracts;
using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Matches;

/// <summary>The running matches (spec §6.6), by id and by player. A player is in at most one, and acts only in their own seat.</summary>
internal sealed class MatchRegistry(IMatchStore store, CardDatabase cards, IHubContext<GameHub, IGameClient> hub, ILogger<MatchHost> hostLog)
{
    public const string NotFound = "There is no such match.";
    public const string UnreadableAction = "That action can't be read.";

    private readonly ConcurrentDictionary<Guid, MatchHost> _hosts = new();
    private readonly ConcurrentDictionary<int, MatchHost> _byUser = new();
    private readonly ConcurrentDictionary<int, MatchEndedNotice> _abandoned = new();

    public int Count => _hosts.Count;

    public bool IsPlaying(int userId) => _byUser.ContainsKey(userId);

    public MatchHost? Find(Guid matchId) => _hosts.GetValueOrDefault(matchId);

    /// <summary>Holds a match that was just created, or reloaded from <paramref name="saved"/>.</summary>
    public MatchHost Open(Guid id, Match match, MatchRecord saved, IReadOnlyList<MatchSeat> seats)
    {
        var host = new MatchHost(id, match, saved, seats, store, cards, hub, hostLog, Close);
        _hosts[id] = host;
        foreach (var seat in seats)
        {
            _byUser[seat.UserId] = host;
            _abandoned.TryRemove(seat.UserId, out _);
        }
        return host;
    }

    /// <summary>The user's running match and their view of it, or nothing.</summary>
    public async Task<MatchReply> CurrentAsync(int userId) =>
        _byUser.TryGetValue(userId, out var host) && host.SeatOf(userId) is { } seat
            ? new MatchReply(host.Id, await host.ViewAsync(seat))
            : MatchReply.None;

    public Guid? MatchOf(int userId) => _byUser.TryGetValue(userId, out var host) ? host.Id : null;

    /// <summary>The notice of a match of the user's abandoned at startup, handed out once.</summary>
    public MatchEndedNotice? TakeAbandoned(int userId) => _abandoned.TryRemove(userId, out var ended) ? ended : null;

    /// <summary>Keeps the notice until the user next asks for their lobby (in memory: a later restart forgets it).</summary>
    public void NoteAbandoned(int userId, MatchEndedNotice ended) => _abandoned[userId] = ended;

    /// <summary>The user acts in their own seat; a match they aren't in reads as missing.</summary>
    public Task<SubmitReply> SubmitAsync(int userId, Guid matchId, PlayerAction? action)
    {
        if (Find(matchId) is not { } host || host.SeatOf(userId) is not { } seat) return Task.FromResult(new SubmitReply(false, null, NotFound));
        if (action is null) return Task.FromResult(new SubmitReply(false, null, UnreadableAction));
        return host.SubmitAsync(seat, action);
    }

    private void Close(MatchHost host)
    {
        _hosts.TryRemove(host.Id, out _);
        foreach (var seat in host.Seats) _byUser.TryRemove(new KeyValuePair<int, MatchHost>(seat.UserId, host));
    }
}
