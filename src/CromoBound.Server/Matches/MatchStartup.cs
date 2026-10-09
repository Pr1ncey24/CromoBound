using CromoBound.Contracts;
using CromoBound.Data;
using CromoBound.Engine.Matches;
using CromoBound.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Matches;

/// <summary>Runs at startup, after the database is ready (spec §6.6). Taking the card data loads it, so a data folder that can't be
/// loaded stops the server. Every Running match is replayed from its record. A match that can't be replayed is marked Abandoned, and
/// its players are told the next time they ask for their match. That covers a record from another engine build or other card data,
/// one that can't be read at all, and one whose player no longer exists. Nothing in a saved match can stop the server from
/// starting.</summary>
internal sealed class MatchStartup(CardDatabase cards, IMatchStore store, MatchRegistry matches, IServiceScopeFactory scopes,
    ILogger<MatchStartup> log) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("Loaded {Count} cards.", cards.Cards.Count);
        var running = await store.RunningAsync();
        if (running.Count == 0) return;
        var names = await UserNamesAsync(cancellationToken);
        foreach (var row in running)
        {
            try
            {
                IReadOnlyList<MatchSeat> seats =
                    [new(row.Seat0UserId, names[row.Seat0UserId]), new(row.Seat1UserId, names[row.Seat1UserId])];
                var record = MatchStore.Read(row.RecordJson);
                matches.Open(row.Id, Match.Load(record, cards), record, seats);
            }
            catch (Exception ex)
            {
                if (ex is MatchVersionMismatchException) log.LogWarning("Match {MatchId} is abandoned: {Reason}", row.Id, ex.Message);
                else log.LogError(ex, "Match {MatchId} can't be reloaded and is abandoned.", row.Id);
                await store.SetStatusAsync(row.Id, MatchStatus.Abandoned);
                var ended = new MatchEndedNotice(row.Id, MatchEndReason.Abandoned, [0, 0], null);
                foreach (var userId in (int[])[row.Seat0UserId, row.Seat1UserId]) matches.NoteAbandoned(userId, ended);
            }
        }
        log.LogInformation("Reloaded {Reloaded} of {Running} running matches.", matches.Count, running.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task<Dictionary<int, string>> UserNamesAsync(CancellationToken cancel)
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CromoDbContext>().Users.AsNoTracking()
            .ToDictionaryAsync(u => u.Id, u => u.UserName, cancel);
    }
}
