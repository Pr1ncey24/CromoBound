using System.Collections.Concurrent;
using Microsoft.Extensions.Options;

namespace CromoBound.Server.Accounts;

/// <summary>Per-username lockout (spec §4.4): after <see cref="ServerOptions.LockoutFailures"/> failures in a row a name is locked
/// for <see cref="ServerOptions.LockoutMinutes"/>, whether or not the account exists. Kept in memory; a restart clears it.</summary>
internal sealed class LoginLockout(TimeProvider time, IOptions<ServerOptions> options)
{
    private sealed record Entry(int Failures, DateTimeOffset? LockedUntil);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public bool IsLocked(string userName) =>
        _entries.TryGetValue(UserStore.Normalize(userName), out var entry) && entry.LockedUntil is { } until && time.GetUtcNow() < until;

    public void RecordFailure(string userName) =>
        _entries.AddOrUpdate(UserStore.Normalize(userName), _ => Next(new Entry(0, null)), (_, entry) => Next(entry));

    public void Reset(string userName) => _entries.TryRemove(UserStore.Normalize(userName), out _);

    /// <summary>One more failure; a lock that has ended starts a fresh count.</summary>
    private Entry Next(Entry entry)
    {
        var now = time.GetUtcNow();
        var settings = options.Value;
        var failures = entry.LockedUntil is { } until && now >= until ? 1 : entry.Failures + 1;
        return failures >= settings.LockoutFailures ? new Entry(0, now.AddMinutes(settings.LockoutMinutes)) : new Entry(failures, null);
    }
}
