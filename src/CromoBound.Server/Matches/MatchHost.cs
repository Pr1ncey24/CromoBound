using CromoBound.Data;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Server.Hubs;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Matches;

/// <summary>One running match (spec §6.6): the engine's match, its two seats and its last saved record. A lock serializes everything
/// that reads or changes the match: an accepted action is saved before anyone sees it, and each player's view is built from a settled
/// state. <paramref name="finished"/> takes the match out of the registry once its final save succeeds.</summary>
internal sealed class MatchHost(Guid id, Match match, MatchRecord saved, IReadOnlyList<MatchSeat> seats, IMatchStore store,
    CardDatabase cards, IHubContext<GameHub, IGameClient> hub, ILogger log, Action<MatchHost> finished)
{
    public const string SaveFailed = "The action couldn't be saved, try again.";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private MatchRecord _saved = saved;

    public Guid Id => id;

    /// <summary>The engine's match; replaced by a reload of the last saved record when a save fails.</summary>
    public Match Match { get; private set; } = match;

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

    /// <summary>Under the lock, the engine decides; a rejected action changes and saves nothing. An accepted one is saved, then each
    /// player is sent their view. If the save fails, the match goes back to its last saved record, the action is lost and the caller is
    /// asked to try again. A finished match is saved as Finished and leaves the registry right after that save, so whatever the pushes do the
    /// players are free to play again; then each is sent their final view and both are told the match ended.</summary>
    public async Task<SubmitReply> SubmitAsync(PlayerId seat, PlayerAction action)
    {
        await _lock.WaitAsync();
        try
        {
            var result = Match.Submit(seat, action);
            if (!result.Accepted) return new SubmitReply(false, result.Rejection, null);
            var record = Match.ToRecord();
            var over = Match.Stage == MatchStage.Over;
            try
            {
                await store.SaveAsync(id, record, over ? MatchStatus.Finished : MatchStatus.Running);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Saving match {MatchId} failed; it goes back to its last saved state.", id);
                Match = Match.Load(_saved, cards);
                return new SubmitReply(false, null, SaveFailed);
            }
            _saved = record;
            if (over) finished(this);
            await PushViewsAsync();
            if (over) await TellEndedAsync();
            return new SubmitReply(true, null, null);
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

    /// <summary>Tells both players the match ended, after their final views.</summary>
    private async Task TellEndedAsync()
    {
        var result = Match.Result;
        var winner = result.Winner is { } seat ? seats[seat.Index].UserName : null;
        await hub.Clients.Groups([.. seats.Select(s => GameHub.UserGroup(s.UserId))])
            .MatchEnded(new MatchEndedNotice(id, MatchEndReason.Finished, result.GameWins, winner));
    }
}
