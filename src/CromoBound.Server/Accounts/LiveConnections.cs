namespace CromoBound.Server.Accounts;

/// <summary>Each user's open hub connections. A hub connection is checked once when it opens, so a change that ends a user's sessions
/// (spec §4.3) must also close their live connections, or a disabled player would keep receiving. Counting them per user also says
/// who is online, and when a user's first connection opens or last one closes.</summary>
internal sealed class LiveConnections
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, (int UserId, Action Abort)> _open = [];

    /// <summary>Tracks a new connection; true when it is the user's first open one.</summary>
    public bool Opened(string connectionId, int userId, Action abort)
    {
        lock (_gate)
        {
            _open[connectionId] = (userId, abort);
            return _open.Values.Count(c => c.UserId == userId) == 1;
        }
    }

    /// <summary>Forgets a connection; true when it was its user's last open one.</summary>
    public bool Closed(string connectionId, out int userId)
    {
        lock (_gate)
        {
            if (!_open.Remove(connectionId, out var connection))
            {
                userId = 0;
                return false;
            }
            userId = connection.UserId;
            var user = userId;
            return !_open.Values.Any(c => c.UserId == user);
        }
    }

    public bool IsOnline(int userId)
    {
        lock (_gate) return _open.Values.Any(c => c.UserId == userId);
    }

    public IReadOnlyList<string> ConnectionsOf(int userId)
    {
        lock (_gate) return [.. _open.Where(c => c.Value.UserId == userId).Select(c => c.Key)];
    }

    /// <summary>Closes every open connection of the user. The aborts run outside the lock.</summary>
    public void EndAll(int userId)
    {
        List<Action> aborts;
        lock (_gate) aborts = [.. _open.Values.Where(c => c.UserId == userId).Select(c => c.Abort)];
        foreach (var abort in aborts) abort();
    }
}
