namespace CromoBound.Client.Services;

/// <summary>The browser's local storage: strings by key, in this browser only.</summary>
public interface IBrowserStorage
{
    ValueTask<string?> GetAsync(string key);

    /// <summary>Throws when the browser refuses the write (its storage is full).</summary>
    ValueTask SetAsync(string key, string value);
}
