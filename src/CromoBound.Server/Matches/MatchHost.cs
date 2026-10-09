using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Matches;

/// <summary>One running match (spec §6.6): the engine's match and its two seats. A lock serializes everything that reads or changes
/// the match, so each player's view is built from a settled state.</summary>
internal sealed class MatchHost(Guid id, Match match, IReadOnlyList<MatchSeat> seats, IHubContext<GameHub, IGameClient> hub)
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    public Guid Id => id;

    public Match Match { get; } = match;

    public IReadOnlyList<MatchSeat> Seats => seats;

    public PlayerId? SeatOf(int userId)
    {
        for (var i = 0; i < seats.Count; i++)
            if (seats[i].UserId == userId) return new PlayerId(i);
        return null;
    }

    /// <summary>Tells each player the match started, then sends each their first view.</summary>
    public async Task StartAsync()
    {
        await _lock.WaitAsync();
        try
        {
            for (var i = 0; i < seats.Count; i++)
                await hub.Clients.Group(GameHub.UserGroup(seats[i].UserId))
                    .MatchStarted(new MatchStartedNotice(id, seats[1 - i].UserName, new PlayerId(i)));
            await PushViewsAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<PlayerView> ViewAsync(PlayerId seat)
    {
        await _lock.WaitAsync();
        try
        {
            return Match.ViewFor(seat);
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Each player gets their own seat's view, and only that.</summary>
    private async Task PushViewsAsync()
    {
        for (var i = 0; i < seats.Count; i++)
            await hub.Clients.Group(GameHub.UserGroup(seats[i].UserId)).View(new MatchViewNotice(id, Match.ViewFor(new PlayerId(i))));
    }
}
