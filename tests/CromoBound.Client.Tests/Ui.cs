using Bunit;
using CromoBound.Client.Services;
using CromoBound.Client.Tests.Fakes;
using CromoBound.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace CromoBound.Client.Tests;

/// <summary>A bUnit context with MudBlazor and the app's services, the server replaced by fakes. Each test makes its own with
/// <c>await using</c>: MudBlazor's services only dispose asynchronously, which a test class's synchronous dispose can't do.</summary>
internal sealed class Ui : IAsyncDisposable
{
    public Ui(MeResponse? me = null, bool signedIn = true)
    {
        me ??= new MeResponse("marco", false);
        Ctx.JSInterop.Mode = JSRuntimeMode.Loose;
        Ctx.Services.AddMudServices();
        if (signedIn) Session.SignedIn(me);
        Api = new FakeServerApi(Session) { Me = me };
        Ctx.Services.AddSingleton(Session);
        Ctx.Services.AddSingleton<IServerApi>(Api);
        Ctx.Services.AddSingleton(TimeProvider.System);
        Ctx.Services.AddSingleton<SessionKeeper>();
    }

    public BunitContext Ctx { get; } = new();
    public SessionState Session { get; } = new();
    public FakeServerApi Api { get; }
    public NavigationManager Nav => Ctx.Services.GetRequiredService<NavigationManager>();

    public ValueTask DisposeAsync() => Ctx.DisposeAsync();
}

internal static class Clicks
{
    /// <summary>Waits for the element, then clicks it on the renderer's thread. Waiting inside <c>InvokeAsync</c> would block the
    /// renderer the wait depends on.</summary>
    public static async Task ClickAsync<T>(this IRenderedComponent<T> cut, string selector) where T : IComponent
    {
        var element = cut.WaitForElement(selector);
        await cut.InvokeAsync(() => element.Click());
    }
}
