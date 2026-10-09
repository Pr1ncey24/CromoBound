using System.Collections.Concurrent;

namespace CromoBound.Server.Accounts;

/// <summary>Each user's open hub connections. A hub connection is checked once when it opens, so a change that ends a user's sessions
/// (spec §4.3) must also close their live connections, or a disabled player would keep receiving.</summary>
internal sealed class LiveConnections
{
    private readonly ConcurrentDictionary<string, (int UserId, Action Abort)> _open = new();

    public void Opened(string connectionId, int userId, Action abort) => _open[connectionId] = (userId, abort);

    public void Closed(string connectionId) => _open.TryRemove(connectionId, out _);

    /// <summary>Closes every open connection of the user.</summary>
    public void EndAll(int userId)
    {
        foreach (var (_, connection) in _open)
            if (connection.UserId == userId) connection.Abort();
    }
}
