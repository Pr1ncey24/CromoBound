using Microsoft.JSInterop;

namespace CromoBound.Client.Services;

public sealed class LocalBrowserStorage(IJSRuntime js) : IBrowserStorage
{
    public ValueTask<string?> GetAsync(string key) => js.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask SetAsync(string key, string value) => js.InvokeVoidAsync("localStorage.setItem", key, value);
}
