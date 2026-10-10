using CromoBound.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

namespace CromoBound.Client;

/// <summary>In a namespace, not top-level statements: the server's tests see the client's internals, and a global <c>Program</c> would
/// clash with the server's.</summary>
internal static class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddMudServices();
        builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<SessionState>();
        builder.Services.AddScoped<IServerApi, ServerApi>();
        builder.Services.AddScoped<SessionKeeper>();
        builder.Services.AddScoped<IGameHub, GameConnection>();
        builder.Services.AddScoped<LobbyState>();
        builder.Services.AddScoped<LobbySync>();
        builder.Services.AddScoped<IBrowserStorage, LocalBrowserStorage>();
        builder.Services.AddScoped<CatalogClient>();
        builder.Services.AddScoped<DeckStore>();

        await builder.Build().RunAsync();
    }
}
