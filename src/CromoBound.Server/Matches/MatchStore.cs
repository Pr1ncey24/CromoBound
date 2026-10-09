using System.Text.Json;
using CromoBound.Engine.Matches;
using CromoBound.Server.Storage;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Matches;

/// <summary>Where matches are kept (spec §6.6). An interface so tests can make saves fail.</summary>
internal interface IMatchStore
{
    Task CreateAsync(Guid id, int seat0UserId, int seat1UserId, MatchRecord record);

    /// <summary>Replaces the record and the status. Throws when the match isn't there or the write fails.</summary>
    Task SaveAsync(Guid id, MatchRecord record, MatchStatus status);

    /// <summary>Changes the status only, keeping the record. Throws when the match isn't there.</summary>
    Task SetStatusAsync(Guid id, MatchStatus status);

    /// <summary>Every Running match, oldest first.</summary>
    Task<IReadOnlyList<MatchEntity>> RunningAsync();
}

/// <summary>The Matches table. Each call uses its own database context, so match hosts can save from any thread.</summary>
internal sealed class MatchStore(IServiceScopeFactory scopes, TimeProvider time) : IMatchStore
{
    public static string Json(MatchRecord record) => JsonSerializer.Serialize(record, ServerJson.Options);

    public static MatchRecord Read(string json) =>
        JsonSerializer.Deserialize<MatchRecord>(json, ServerJson.Options) ?? throw new JsonException("A match record was null.");

    public async Task CreateAsync(Guid id, int seat0UserId, int seat1UserId, MatchRecord record)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CromoDbContext>();
        var now = time.GetUtcNow().UtcDateTime;
        db.Matches.Add(new MatchEntity
        {
            Id = id, Seat0UserId = seat0UserId, Seat1UserId = seat1UserId, RecordJson = Json(record),
            Status = MatchStatus.Running, CreatedAt = now, UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    public async Task SaveAsync(Guid id, MatchRecord record, MatchStatus status)
    {
        using var scope = scopes.CreateScope();
        var json = Json(record);
        var now = time.GetUtcNow().UtcDateTime;
        var changed = await Rows(scope, id).ExecuteUpdateAsync(set => set
            .SetProperty(m => m.RecordJson, json)
            .SetProperty(m => m.Status, status)
            .SetProperty(m => m.UpdatedAt, now));
        if (changed != 1) throw new InvalidOperationException($"Match {id} isn't in the database.");
    }

    public async Task SetStatusAsync(Guid id, MatchStatus status)
    {
        using var scope = scopes.CreateScope();
        var now = time.GetUtcNow().UtcDateTime;
        var changed = await Rows(scope, id).ExecuteUpdateAsync(set => set
            .SetProperty(m => m.Status, status)
            .SetProperty(m => m.UpdatedAt, now));
        if (changed != 1) throw new InvalidOperationException($"Match {id} isn't in the database.");
    }

    public async Task<IReadOnlyList<MatchEntity>> RunningAsync()
    {
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CromoDbContext>().Matches.AsNoTracking()
            .Where(m => m.Status == MatchStatus.Running).OrderBy(m => m.CreatedAt).ToListAsync();
    }

    private static IQueryable<MatchEntity> Rows(IServiceScope scope, Guid id) =>
        scope.ServiceProvider.GetRequiredService<CromoDbContext>().Matches.Where(m => m.Id == id);
}
