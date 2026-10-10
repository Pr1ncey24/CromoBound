using Bunit;
using Bunit.TestDoubles;
using CromoBound.Client.Layout;
using CromoBound.Client.Services;
using CromoBound.Client.Tests.Fakes;
using CromoBound.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
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
        Lobby = new LobbyState(Session);
        Ctx.Services.AddSingleton<IGameHub>(Hub);
        Ctx.Services.AddSingleton(Lobby);
        Ctx.Services.AddSingleton<LobbySync>();
        Catalog = new CatalogClient(Api);
        Ctx.Services.AddSingleton(Catalog);
        Decks = new DeckStore(Storage, Session, Catalog);
        Ctx.Services.AddSingleton<IBrowserStorage>(Storage);
        Ctx.Services.AddSingleton(Decks);
    }

    public BunitContext Ctx { get; } = new();
    public SessionState Session { get; } = new();
    public FakeServerApi Api { get; }
    public FakeGameHub Hub { get; } = new();
    public LobbyState Lobby { get; }
    public MemoryStorage Storage { get; } = new();
    public CatalogClient Catalog { get; }
    public DeckStore Decks { get; }
    public BunitNavigationManager Nav => (BunitNavigationManager)Ctx.Services.GetRequiredService<NavigationManager>();

    public IReadOnlyList<string> Notices =>
        [.. Ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars.Select(s => s.Message ?? "")];

    /// <summary>Dialogs show in the provider, which a page rendered on its own doesn't have.</summary>
    public IRenderedComponent<MudDialogProvider> RenderDialogs() => Ctx.Render<MudDialogProvider>();

    /// <summary>A page inside the real layout, which brings its own providers, banners and global navigation.</summary>
    public IRenderedComponent<MainLayout> RenderInLayout<TPage>() where TPage : IComponent =>
        Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, b =>
        {
            b.OpenComponent<TPage>(0);
            b.CloseComponent();
        }));

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
