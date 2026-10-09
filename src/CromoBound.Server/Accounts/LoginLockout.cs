using Microsoft.Extensions.Options;

namespace CromoBound.Server.Accounts;

/// <summary>Per-username lockout (spec §4.4): after <see cref="ServerOptions.LockoutFailures"/> failures in a row a name is locked
/// for <see cref="ServerOptions.LockoutMinutes"/>, whether or not the account exists. An attempt is reserved before its password
/// check, so parallel guesses can't get more checks than the limit allows. Kept in memory; a restart clears it.</summary>
internal sealed class LoginLockout(TimeProvider time, IOptions<ServerOptions> options)
{
    /// <summary>Failures in a row, attempts reserved and not yet finished, and the end of the lock if there is one.</summary>
    private sealed record Entry(int Failures, int InFlight, DateTimeOffset? LockedUntil);

    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    /// <summary>Reserves one attempt at the name, or refuses while the name is locked or while the failures so far plus the
    /// attempts in flight already reach the limit. Every reserved attempt ends with <see cref="RecordFailure"/> or
    /// <see cref="Reset"/>.</summary>
    public bool TryBegin(string userName)
    {
        var key = UserStore.Normalize(userName);
        lock (_gate)
        {
            var entry = Current(key);
            if (entry.LockedUntil is not null || entry.Failures + entry.InFlight >= options.Value.LockoutFailures) return false;
            _entries[key] = entry with { InFlight = entry.InFlight + 1 };
            return true;
        }
    }

    /// <summary>A reserved attempt failed: it stays counted, and the name locks when the failures reach the limit.</summary>
    public void RecordFailure(string userName)
    {
        var key = UserStore.Normalize(userName);
        lock (_gate)
        {
            var entry = Released(Current(key));
            if (entry.LockedUntil is not null)
            {
                _entries[key] = entry;
                return;
            }
            var settings = options.Value;
            var failures = entry.Failures + 1;
            _entries[key] = failures >= settings.LockoutFailures
                ? new Entry(0, entry.InFlight, time.GetUtcNow().AddMinutes(settings.LockoutMinutes))
                : entry with { Failures = failures };
        }
    }

    /// <summary>A reserved attempt succeeded: the count starts again (other attempts still in flight stay reserved).</summary>
    public void Reset(string userName)
    {
        var key = UserStore.Normalize(userName);
        lock (_gate)
        {
            var entry = Released(Current(key)) with { Failures = 0, LockedUntil = null };
            if (entry.InFlight == 0) _entries.Remove(key);
            else _entries[key] = entry;
        }
    }

    /// <summary>The name's state now; a lock that has ended starts a fresh count.</summary>
    private Entry Current(string key)
    {
        if (!_entries.TryGetValue(key, out var entry)) return new Entry(0, 0, null);
        return entry.LockedUntil is { } until && time.GetUtcNow() >= until ? entry with { Failures = 0, LockedUntil = null } : entry;
    }

    private static Entry Released(Entry entry) => entry with { InFlight = Math.Max(0, entry.InFlight - 1) };
}
