namespace CromoBound.Server.Matches;

/// <summary>The maintenance switch (spec §6.7): in memory, off at startup. While it's on, no challenge is made or accepted; running
/// matches go on, so they can finish before a deploy.</summary>
internal sealed class Maintenance
{
    private volatile bool _on;

    public bool On
    {
        get => _on;
        set => _on = value;
    }
}
