using CromoBound.Client.Services;

namespace CromoBound.Client.Tests.Fakes;

internal sealed class MemoryStorage : IBrowserStorage
{
    public Dictionary<string, string> Items { get; } = [];

    /// <summary>When set, writes fail as a full browser storage does.</summary>
    public bool Full { get; set; }

    public ValueTask<string?> GetAsync(string key) => ValueTask.FromResult(Items.GetValueOrDefault(key));

    public ValueTask SetAsync(string key, string value)
    {
        if (Full) throw new InvalidOperationException("The quota has been exceeded.");
        Items[key] = value;
        return ValueTask.CompletedTask;
    }
}
