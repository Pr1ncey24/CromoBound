# Client Plan J: the Phase 4a App

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Second of two plans for Phase 4a (I: server side, done; J: the client app).

**Goal:** Friends sign in, see each other in the lobby, challenge with a deck pasted into the browser, land on the match page and can concede. Admins manage users and maintenance. All of it uses the approved look: rounded shapes, the Crimson night palette, Fredoka titles and Nunito text.

**Architecture:**
- **`CromoBound.Client`:** a Blazor WebAssembly app on MudBlazor 9.
  - MudBlazor supplies the theme, buttons, dialogs and notifications. Layout and tiles are plain HTML with the app's own CSS (`wwwroot/css/app.css`), following the mocks.
  - Services sit behind small interfaces, so pages are tested with fakes:
    - `IServerApi` covers HTTP (`/api/me`, `/logout`, admin users, maintenance).
    - `IGameHub` covers the SignalR connection: calls, notices, connection state.
    - `IBrowserStorage` covers local storage.
  - `SessionState` holds who is signed in and ends the session on any 401. `SessionKeeper` renews the session every 30 minutes while the app is open.
  - `LobbyState` folds the lobby's notices into one state the pages render. `LobbySync` feeds it from the hub and reloads it with `GetLobby` after every (re)connect.
  - `DeckStore` keeps decks in local storage per username.
- **One server change (Task 1):** `GetMatch` also returns the opponent's name, so the match page can show it after a reload.
- **Routes:** the server already serves every page this plan adds (Plan I's `ClientApp` list: `/`, `/decks`, `/admin/users`, `/admin/maintenance`, `/match/{id}`). A new page outside that list would need it extended.

**Tech Stack:**
- .NET 10: Blazor WebAssembly 10.0.12, the SignalR client 10.0.12.
- MudBlazor 9.11.0.
- Tests: xUnit 2.9.3 and bUnit 2.11.3.

**Spec:** `docs/client.md` (sections 5 to 9 and 11, 12 client bullets, 13 row J).
- Design: the approved mocks, the "Screens" tab of https://claude.ai/artifact/YCkWxnsoXJAJcKY4z3z7QT.
- This plan's CSS and markup carry their look; the plan is the authority where the two differ.

## Global Constraints

- **Language:** `net10.0` with nullable enabled and implicit usings (from `Directory.Build.props`). Client types are `public` or `internal` as Razor needs; components are `public` (Razor's default).
- **Privacy:**
  - The app is only ever served to signed-in players (Plan I).
  - The page title stays "Play".
  - Nothing the app shows names a role.
  - The browser's error bar says "Something went wrong. Reload the page." and nothing else.
- **Look (the approved mocks):**
  - **Fonts:** Fredoka for titles and the logo; Nunito for everything else.
  - **Colors:** ground `#0b0909`, panels `#161112`, raised `#211819`, top bar `#1a1314`.
  - **Text:** `#f2e9e6`, muted `#b39d99`.
  - **Accents:** primary buttons `#d8392a` with white text; links and accents `#ff8a7a` / `#ff7a6b`.
  - **Danger:** rose (`#b8284f` fill, `#ff9db0` text).
  - **Status:** online `#4fc3a1`, in a match `#e3913f`.
  - **Banners:** maintenance is blue (`#14243a`, text `#bcd6f5`), reconnecting is warm (`#2b1712`, text `#ffb0a5`).
  - **Shapes:** buttons and the top bar are pills, panels have 24px corners, dialogs 28px, inputs 14px.
  - The top bar floats: a rounded capsule with 16px space above it and 24px at the sides.
- **Copy:**
  - Plain English sentences, as in the mocks.
  - The server's own messages (refusals, deck problems) are shown word for word.
- **Accessibility:**
  - Real `<button>`, `<a href>`, `<input>` and `<label>` elements, with `aria-label` on anything icon-only.
  - Touch targets of at least 44px.
  - Text contrast of at least 4.5:1.
- **Packages:**
  - The client adds only MudBlazor 9.11.0 and `Microsoft.AspNetCore.SignalR.Client` 10.0.12 (plus the existing WebAssembly package).
  - The test project adds only `bunit` 2.11.3.
  - The engine, models, data and contracts projects stay package-free.
- **MudBlazor's analyzers:**
  - Its components take a lowercase `id` attribute, never `Id`; the analyzer warns about `Id`.
  - Every warning counts toward the zero-warnings rule.
- **bUnit:**
  - Every test makes its own context with `await using var ui = new Ui();` (Task 2's test helper). A test class must not inherit `BunitContext`: MudBlazor services can only be disposed asynchronously, and xUnit disposes test classes synchronously.
  - Click with Task 2's `ClickAsync(selector)` extension. It waits for the element outside the renderer and then clicks on the renderer's thread; a wait inside `InvokeAsync` would block the renderer it waits for.
- **Owner rules:**
  - 0 build warnings and 0 errors at all times.
  - Conventional, title-only commit messages: no body, no co-author trailer, no mention of Claude/AI.
  - No em dashes or en dashes in code, comments, strings or markup.
  - LF line endings; UTF-8 without BOM.
- **dotnet:** run it with `export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && ` in Git Bash. The default dotnet on PATH is SDK 9. The build reports in Italian: `Avvisi` = warnings, `Errori` = errors, `Superato` = passed.

## Deliberate deviations from the spec and the mocks (reviewers: these are intended)

1. **No Cards link in the top bar.** The Cards page is a 4c preview in the mocks; 4a's bar shows Play, Decks and (for admins) Admin.
2. **`GetMatch` gains the opponent's name (`MatchReply.Opponent`).** Without it, a reloaded match page couldn't say who you play.
3. **Notices become short notifications:**
   - a challenge received;
   - your challenge declined;
   - a challenge withdrawn or cancelled.

   `LobbyState` writes them, and the layout shows them on every page. Starting a match, and an abandoned match, go to the match page or a dialog from any page, not only the lobby.
4. **Deleting a deck asks nothing.** The decks live in this browser only and are pasted from files the player keeps.
5. **Admin dialogs call the server themselves** and show its refusal inside the dialog, so a mistyped password keeps what was typed. The pages reload the user list after every change.
6. **The board, card zoom, mulligan and Cards artboards are not built here.** They are previews for 4b and 4c.
7. **A new challenge shows only in the tab that sent it.** The server tells the challenged player, not the challenger's other tabs (Plan I); those tabs see it after their next reconnect or reload.

## Review Focus

1. **A session that ends while the app is open** (the account is disabled, the password changes, or the laptop sleeps past the lifetime). Any 401 sends the app to `/login` exactly once, never into a loop of requests. Pinned in Task 2 (`A_401_ends_the_session_once_and_the_layout_goes_to_login`).
2. **Pasted text that isn't a deck:** garbage, valid JSON that lacks the deck's fields, a deck without a name, or a file over 1 MB. Each gives a plain error and saves nothing. Pinned in Task 4 (`Text_that_isnt_a_deck_is_refused_and_nothing_is_saved`, `A_file_that_is_too_big_is_refused`).
3. **Two accounts sharing one browser** never see each other's decks. Pinned in Task 4 (`Decks_are_kept_per_user`).
4. **The hub is reconnecting:** the banner shows, and Challenge and Accept are disabled, so nothing is sent into a dead connection. Pinned in Task 5 (`While_reconnecting_the_banner_shows_and_challenging_is_off`).
5. **Opening `/match/{id}` for a match that isn't yours, or is over,** goes back to the lobby instead of showing a broken page. Pinned in Task 6 (`A_match_that_isnt_yours_goes_back_to_the_lobby`).

---

## File Structure

```
src/CromoBound.Contracts/HubContracts.cs            (modify: MatchReply.Opponent)
src/CromoBound.Server/Matches/MatchRegistry.cs      (modify: the opponent in CurrentAsync)
src/CromoBound.Client/
  CromoBound.Client.csproj                          (modify: Contracts, MudBlazor, SignalR client)
  _Imports.razor
  Program.cs, App.razor                             (replace)
  CrimsonTheme.cs                                   the MudBlazor theme
  Formats.cs                                        names for formats and stages
  Services/ApiResult.cs, IServerApi.cs, ServerApi.cs, SessionState.cs, SessionKeeper.cs
  Services/IGameHub.cs, GameConnection.cs, ForeverRetryPolicy.cs, LobbyState.cs, LobbySync.cs
  Services/IBrowserStorage.cs, LocalBrowserStorage.cs, DeckStore.cs
  Layout/MainLayout.razor                           top bar, banners, notifications, global navigation
  Layout/AdminTabs.razor
  Pages/Lobby.razor, Decks.razor, Match.razor, Admin/Users.razor, Admin/Maintenance.razor
  Dialogs/DialogResults.cs                          the records dialogs return, and their shared options
  Dialogs/ChallengeDialog.razor, AcceptDialog.razor, DeckProblemsDialog.razor, AbandonedDialog.razor,
          ConcedeDialog.razor, ResultDialog.razor, CreateUserDialog.razor, SetPasswordDialog.razor
  wwwroot/index.html                                (modify: fonts, MudBlazor, app.css)
  wwwroot/css/app.css
tests/CromoBound.Client.Tests/
  CromoBound.Client.Tests.csproj
  Ui.cs                                             the test context, its fakes, and ClickAsync
  SampleDecks.cs, Views.cs                          deck JSON and player views for the tests
  Fakes/FakeServerApi.cs, FakeGameHub.cs, MemoryStorage.cs, ManualTimeProvider.cs, StubHandler.cs
  ServerApiTests.cs, SessionKeeperTests.cs, LayoutTests.cs, LobbyStateTests.cs, LobbySyncTests.cs,
  RetryPolicyTests.cs, DeckStoreTests.cs, DecksPageTests.cs, LobbyPageTests.cs, MatchPageTests.cs,
  AdminPagesTests.cs
docs/client-manual-checks.md
CromoBound.slnx                                     (modify)
```

---

### Task 1: The match reply names the opponent

**Files:**
- Modify: `src/CromoBound.Contracts/HubContracts.cs`, `src/CromoBound.Server/Matches/MatchRegistry.cs`
- Test: `tests/CromoBound.Server.Tests/MatchStartTests.cs`

**Interfaces:**
- Consumes: Plan I's `MatchReply(Guid? MatchId, PlayerView? View)` and `MatchRegistry.CurrentAsync`.
- Produces: `MatchReply(Guid? MatchId, PlayerView? View, string? Opponent)`, with `MatchReply.None` = all null.

- [ ] **Step 1: Write the failing test**

Add to `tests/CromoBound.Server.Tests/MatchStartTests.cs`:

```csharp
    [Fact]
    public async Task GetMatch_names_the_opponent()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);

        Assert.Equal("bob", (await players.First.GetMatchAsync()).Opponent);
        Assert.Equal("alice", (await players.Second.GetMatchAsync()).Opponent);
    }
```

- [ ] **Step 2: Run it to verify it fails**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`MatchReply` has no `Opponent`).

- [ ] **Step 3: Write the implementation**

In `src/CromoBound.Contracts/HubContracts.cs`, replace `MatchReply` with:

```csharp
/// <summary>GetMatch's answer: the player's running match, their view of it and their opponent's name, or nothing.</summary>
public sealed record MatchReply(Guid? MatchId, PlayerView? View, string? Opponent)
{
    public static MatchReply None { get; } = new(null, null, null);
}
```

In `src/CromoBound.Server/Matches/MatchRegistry.cs`, replace `CurrentAsync` with:

```csharp
    /// <summary>The user's running match, their view of it and their opponent's name, or nothing.</summary>
    public async Task<MatchReply> CurrentAsync(int userId) =>
        _byUser.TryGetValue(userId, out var host) && host.SeatOf(userId) is { } seat
            ? new MatchReply(host.Id, await host.ViewAsync(seat), host.Seats[1 - seat.Index].UserName)
            : MatchReply.None;
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Contracts src/CromoBound.Server tests/CromoBound.Server.Tests
git commit -m "feat(server): name the opponent in the match reply"
```

---

### Task 2: The app's foundation: theme, HTTP, session and the top bar

**Files:**
- Modify: `src/CromoBound.Client/CromoBound.Client.csproj`, `Program.cs`, `App.razor`, `wwwroot/index.html`, `CromoBound.slnx`
- Create:
  - `src/CromoBound.Client/_Imports.razor`, `CrimsonTheme.cs`, `wwwroot/css/app.css`
  - `src/CromoBound.Client/Services/ApiResult.cs`, `Services/IServerApi.cs`, `Services/ServerApi.cs`, `Services/SessionState.cs`, `Services/SessionKeeper.cs`
  - `src/CromoBound.Client/Layout/MainLayout.razor`, `Pages/Lobby.razor` (a placeholder, which Task 5 replaces)
  - `tests/CromoBound.Client.Tests/CromoBound.Client.Tests.csproj`, `Ui.cs`, `Fakes/FakeServerApi.cs`, `Fakes/ManualTimeProvider.cs`, `Fakes/StubHandler.cs`
  - `tests/CromoBound.Client.Tests/ServerApiTests.cs`, `SessionKeeperTests.cs`, `LayoutTests.cs`

**Interfaces:**
- Consumes:
  - From the contracts: `MeResponse`, `ErrorResponse`, `UserSummary`, `CreateUserRequest`, `PasswordRequest`, `RoleRequest`, `DisabledRequest`, `MaintenanceRequest`, `MaintenanceStatus`.
  - The server routes `GET /api/me`, `POST /logout`, `/api/admin/users...` and `/api/admin/maintenance`.
- Produces:
  - `ApiResult` (`Error`, `Ok`) and `ApiResult<T>` (`Value`, `Error`, `Ok`).
  - `IServerApi`:
    - `MeAsync()`, `LogoutAsync()`;
    - `UsersAsync()`, `CreateUserAsync(CreateUserRequest)`;
    - `SetPasswordAsync(int, string)`, `SetAdminAsync(int, bool)`, `SetDisabledAsync(int, bool)`;
    - `MaintenanceAsync()`, `SetMaintenanceAsync(bool)`.
  - `ServerApi`, with the constants `Forbidden`, `Unexpected` and `NotReachable`.
  - `SessionState`: `Me`, `SignedIn(MeResponse)`, `End()`, `IsEnded`, the `SessionEnded` event and the constant `Ended`.
  - `SessionKeeper`: `Interval`, `Start()`, `PingAsync()`.
  - `CrimsonTheme.Theme`.
  - `MainLayout`.
  - In the tests:
    - `Ui` (`Ctx`, `Session`, `Api`, `Nav`), made with `new Ui(me, signedIn)`;
    - the extension `ClickAsync(selector)` on any rendered component;
    - `FakeServerApi`, `ManualTimeProvider`, `StubHandler`.

- [ ] **Step 1: Set up the projects**

In `src/CromoBound.Client/CromoBound.Client.csproj`, replace the `ItemGroup` with:

```xml
  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.12" />
    <PackageReference Include="Microsoft.AspNetCore.SignalR.Client" Version="10.0.12" />
    <PackageReference Include="MudBlazor" Version="9.11.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\CromoBound.Contracts\CromoBound.Contracts.csproj" />
  </ItemGroup>

  <ItemGroup>
    <InternalsVisibleTo Include="CromoBound.Client.Tests" />
  </ItemGroup>
```

Create `tests/CromoBound.Client.Tests/CromoBound.Client.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Razor">

  <PropertyGroup>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="bunit" Version="2.11.3" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>

  <ItemGroup>
    <Using Include="Xunit" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\CromoBound.Client\CromoBound.Client.csproj" />
  </ItemGroup>

</Project>
```

Add it to `CromoBound.slnx`, in the `/tests/` folder, keeping alphabetical order:

```xml
    <Project Path="tests/CromoBound.Client.Tests/CromoBound.Client.Tests.csproj" />
```

Create `src/CromoBound.Client/_Imports.razor`:

```razor
@using System.Net.Http
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using MudBlazor
@using CromoBound.Contracts
@using CromoBound.Client
@using CromoBound.Client.Services
@using CromoBound.Client.Layout
```

Task 5 adds `@using CromoBound.Client.Dialogs` here, with the first dialog.

- [ ] **Step 2: Write the failing tests**

Create `tests/CromoBound.Client.Tests/Fakes/StubHandler.cs`:

```csharp
using System.Net;
using System.Text;

namespace CromoBound.Client.Tests.Fakes;

/// <summary>An HTTP handler that answers every request with one status and body, and remembers the requests.</summary>
internal sealed class StubHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
```

Create `tests/CromoBound.Client.Tests/Fakes/ManualTimeProvider.cs`:

```csharp
namespace CromoBound.Client.Tests.Fakes;

/// <summary>A clock whose timers fire only when the test says so.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    public IReadOnlyList<TimeSpan> Periods => [.. _timers.Select(t => t.Period)];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, period);
        lock (_timers) _timers.Add(timer);
        return timer;
    }

    /// <summary>Fires every timer once.</summary>
    public void Tick()
    {
        List<ManualTimer> timers;
        lock (_timers) timers = [.. _timers];
        foreach (var timer in timers) timer.Fire();
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan period) : ITimer
    {
        public TimeSpan Period { get; private set; } = period;

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Period = period;
            return true;
        }

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
```

Create `tests/CromoBound.Client.Tests/Fakes/FakeServerApi.cs`:

```csharp
using CromoBound.Client.Services;
using CromoBound.Contracts;

namespace CromoBound.Client.Tests.Fakes;

/// <summary>The server's HTTP side, in memory. A test sets what it answers and reads what was asked.</summary>
internal sealed class FakeServerApi(SessionState session) : IServerApi
{
    public MeResponse Me { get; set; } = new("marco", false);

    /// <summary>When set, every call behaves like a 401: the session ends.</summary>
    public bool SessionOver { get; set; }

    /// <summary>When set, the next command fails with this message (once).</summary>
    public string? NextError { get; set; }

    public List<UserSummary> Users { get; } = [];
    public MaintenanceStatus Maintenance { get; set; } = new(false, 0);
    public List<string> Calls { get; } = [];
    public int MeCalls { get; private set; }

    public Task<ApiResult<MeResponse>> MeAsync()
    {
        MeCalls++;
        return Task.FromResult(Answer(() => Me));
    }

    public Task<ApiResult> LogoutAsync() => Task.FromResult(Command("logout"));

    public Task<ApiResult<IReadOnlyList<UserSummary>>> UsersAsync() =>
        Task.FromResult(Answer<IReadOnlyList<UserSummary>>(() => [.. Users]));

    public Task<ApiResult<UserSummary>> CreateUserAsync(CreateUserRequest request)
    {
        var result = Command($"create {request.UserName} {request.IsAdmin}");
        if (!result.Ok) return Task.FromResult(new ApiResult<UserSummary>(default, result.Error));
        var user = new UserSummary(Users.Count + 1, request.UserName!, request.IsAdmin, false);
        Users.Add(user);
        return Task.FromResult(new ApiResult<UserSummary>(user, null));
    }

    public Task<ApiResult> SetPasswordAsync(int id, string password) => Task.FromResult(Command($"password {id}"));

    public Task<ApiResult> SetAdminAsync(int id, bool isAdmin) => Task.FromResult(Command($"admin {id} {isAdmin}", () =>
        Users[Users.FindIndex(u => u.Id == id)] = Users.Single(u => u.Id == id) with { IsAdmin = isAdmin }));

    public Task<ApiResult> SetDisabledAsync(int id, bool disabled) => Task.FromResult(Command($"disabled {id} {disabled}", () =>
        Users[Users.FindIndex(u => u.Id == id)] = Users.Single(u => u.Id == id) with { Disabled = disabled }));

    public Task<ApiResult<MaintenanceStatus>> MaintenanceAsync() => Task.FromResult(Answer(() => Maintenance));

    public Task<ApiResult<MaintenanceStatus>> SetMaintenanceAsync(bool on)
    {
        var result = Command($"maintenance {on}", () => Maintenance = Maintenance with { On = on });
        return Task.FromResult(result.Ok ? new ApiResult<MaintenanceStatus>(Maintenance, null) : new ApiResult<MaintenanceStatus>(default, result.Error));
    }

    private ApiResult<T> Answer<T>(Func<T> value)
    {
        if (SessionOver)
        {
            session.End();
            return new ApiResult<T>(default, SessionState.Ended);
        }
        return new ApiResult<T>(value(), null);
    }

    private ApiResult Command(string call, Action? apply = null)
    {
        Calls.Add(call);
        if (SessionOver)
        {
            session.End();
            return new ApiResult(SessionState.Ended);
        }
        if (NextError is { } error)
        {
            NextError = null;
            return new ApiResult(error);
        }
        apply?.Invoke();
        return ApiResult.Success;
    }
}
```

Create `tests/CromoBound.Client.Tests/Ui.cs`:

```csharp
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
```

Create `tests/CromoBound.Client.Tests/ServerApiTests.cs`:

```csharp
using System.Net;
using CromoBound.Client.Services;
using CromoBound.Client.Tests.Fakes;

namespace CromoBound.Client.Tests;

public class ServerApiTests
{
    private static (ServerApi Api, SessionState Session, StubHandler Handler) Api(HttpStatusCode status, string body = "")
    {
        var handler = new StubHandler(status, body);
        var session = new SessionState();
        return (new ServerApi(new HttpClient(handler) { BaseAddress = new Uri("https://play.test/") }, session), session, handler);
    }

    [Fact]
    public async Task Me_reads_the_signed_in_user()
    {
        var (api, _, handler) = Api(HttpStatusCode.OK, """{"userName":"marco","canManageUsers":true}""");

        var me = await api.MeAsync();

        Assert.True(me.Ok);
        Assert.Equal(("marco", true), (me.Value!.UserName, me.Value.CanManageUsers));
        Assert.Equal("https://play.test/api/me", handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task A_401_ends_the_session_once()
    {
        var (api, session, _) = Api(HttpStatusCode.Unauthorized);
        var ended = 0;
        session.SessionEnded += () => ended++;

        var first = await api.MeAsync();
        var second = await api.UsersAsync();

        Assert.Equal(SessionState.Ended, first.Error);
        Assert.Equal(SessionState.Ended, second.Error);
        Assert.True(session.IsEnded);
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task A_refusal_carries_the_servers_message()
    {
        var (api, _, _) = Api(HttpStatusCode.BadRequest, """{"error":"That username is taken."}""");

        var result = await api.CreateUserAsync(new("giulia", "a-long-password-1", false));

        Assert.Equal("That username is taken.", result.Error);
    }

    [Fact]
    public async Task A_403_is_a_plain_refusal_and_anything_else_is_generic()
    {
        var (forbidden, _, _) = Api(HttpStatusCode.Forbidden);
        var (broken, _, _) = Api(HttpStatusCode.InternalServerError, "oops");

        Assert.Equal(ServerApi.Forbidden, (await forbidden.UsersAsync()).Error);
        Assert.Equal(ServerApi.Unexpected, (await broken.UsersAsync()).Error);
    }

    [Fact]
    public async Task Commands_send_the_right_method_route_and_body()
    {
        var (api, _, handler) = Api(HttpStatusCode.NoContent);

        await api.SetDisabledAsync(7, true);
        await api.LogoutAsync();

        Assert.Equal((HttpMethod.Put, "https://play.test/api/admin/users/7/disabled"), (handler.Requests[0].Method, handler.Requests[0].RequestUri!.ToString()));
        Assert.Equal("""{"disabled":true}""", await handler.Requests[0].Content!.ReadAsStringAsync());
        Assert.Equal((HttpMethod.Post, "https://play.test/logout"), (handler.Requests[1].Method, handler.Requests[1].RequestUri!.ToString()));
    }
}
```

Create `tests/CromoBound.Client.Tests/SessionKeeperTests.cs`:

```csharp
using CromoBound.Client.Services;
using CromoBound.Client.Tests.Fakes;

namespace CromoBound.Client.Tests;

public class SessionKeeperTests
{
    [Fact]
    public async Task The_keeper_pings_the_server_every_thirty_minutes()
    {
        var session = new SessionState();
        var api = new FakeServerApi(session);
        var time = new ManualTimeProvider();
        await using var keeper = new SessionKeeper(api, time);

        keeper.Start();
        keeper.Start();
        await WaitUntilAsync(() => time.Periods.Count == 1);
        time.Tick();
        await WaitUntilAsync(() => api.MeCalls == 1);
        time.Tick();
        await WaitUntilAsync(() => api.MeCalls == 2);

        Assert.Equal(TimeSpan.FromMinutes(30), SessionKeeper.Interval);
        Assert.Equal(new[] { SessionKeeper.Interval }, time.Periods);
    }

    [Fact]
    public async Task A_ping_that_finds_the_session_over_ends_it()
    {
        var session = new SessionState();
        var api = new FakeServerApi(session) { SessionOver = true };
        await using var keeper = new SessionKeeper(api, TimeProvider.System);

        await keeper.PingAsync();

        Assert.True(session.IsEnded);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 200 && !condition(); i++) await Task.Delay(10);
        Assert.True(condition());
    }
}
```

Create `tests/CromoBound.Client.Tests/LayoutTests.cs`:

```csharp
using Bunit;
using CromoBound.Client.Layout;
using CromoBound.Contracts;

namespace CromoBound.Client.Tests;

public class LayoutTests
{
    [Fact]
    public async Task The_top_bar_shows_the_player_and_no_admin_link_to_a_player()
    {
        await using var ui = new Ui();

        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));

        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#page")));
        Assert.Contains("marco", cut.Find(".cb-bar").TextContent);
        Assert.Equal(new[] { "Play", "Decks" }, cut.FindAll(".cb-nav a").Select(a => a.TextContent.Trim()));
    }

    [Fact]
    public async Task An_admin_also_gets_the_admin_link()
    {
        await using var ui = new Ui(new MeResponse("admin", true));

        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));

        cut.WaitForAssertion(() => Assert.Equal(new[] { "Play", "Decks", "Admin" }, cut.FindAll(".cb-nav a").Select(a => a.TextContent.Trim())));
    }

    [Fact]
    public async Task A_401_ends_the_session_once_and_the_layout_goes_to_login()
    {
        await using var ui = new Ui(signedIn: false);
        ui.Api.SessionOver = true;

        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));

        cut.WaitForAssertion(() => Assert.Equal("http://localhost/login", ui.Nav.Uri));
        Assert.Empty(cut.FindAll("#page"));
        Assert.Equal(1, ui.Api.MeCalls);
    }

    [Fact]
    public async Task Signing_out_posts_logout_and_goes_to_login()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));
        cut.WaitForAssertion(() => Assert.NotNull(cut.Find("#page")));

        await cut.ClickAsync("#sign-out");

        Assert.Contains("logout", ui.Api.Calls);
        Assert.Equal("http://localhost/login", ui.Nav.Uri);
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`ServerApi`, `SessionState`, `SessionKeeper` and `MainLayout` don't exist).

- [ ] **Step 4: Write the implementation**

Create `src/CromoBound.Client/Services/ApiResult.cs`:

```csharp
namespace CromoBound.Client.Services;

/// <summary>A command's outcome: success, or the message to show.</summary>
public sealed record ApiResult(string? Error)
{
    public static ApiResult Success { get; } = new((string?)null);

    public bool Ok => Error is null;
}

/// <summary>A query's outcome: the value, or the message to show.</summary>
public sealed record ApiResult<T>(T? Value, string? Error)
{
    public bool Ok => Error is null;
}
```

Create `src/CromoBound.Client/Services/SessionState.cs`:

```csharp
using CromoBound.Contracts;

namespace CromoBound.Client.Services;

/// <summary>Who is signed in. Any 401 ends the session, once: the layout then goes to the login page (spec §5.1).</summary>
public sealed class SessionState
{
    public const string Ended = "Your session has ended.";

    public MeResponse? Me { get; private set; }

    public bool IsEnded { get; private set; }

    public event Action? SessionEnded;

    public void SignedIn(MeResponse me) => Me = me;

    public void End()
    {
        if (IsEnded) return;
        IsEnded = true;
        SessionEnded?.Invoke();
    }
}
```

Create `src/CromoBound.Client/Services/IServerApi.cs`:

```csharp
using CromoBound.Contracts;

namespace CromoBound.Client.Services;

/// <summary>The server's HTTP endpoints (spec §5, §9). Every call answers with a result; none throws.</summary>
public interface IServerApi
{
    Task<ApiResult<MeResponse>> MeAsync();
    Task<ApiResult> LogoutAsync();
    Task<ApiResult<IReadOnlyList<UserSummary>>> UsersAsync();
    Task<ApiResult<UserSummary>> CreateUserAsync(CreateUserRequest request);
    Task<ApiResult> SetPasswordAsync(int userId, string password);
    Task<ApiResult> SetAdminAsync(int userId, bool isAdmin);
    Task<ApiResult> SetDisabledAsync(int userId, bool disabled);
    Task<ApiResult<MaintenanceStatus>> MaintenanceAsync();
    Task<ApiResult<MaintenanceStatus>> SetMaintenanceAsync(bool on);
}
```

Create `src/CromoBound.Client/Services/ServerApi.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CromoBound.Contracts;

namespace CromoBound.Client.Services;

/// <summary>The HTTP endpoints over the app's own origin, so the session cookie goes along. A 401 ends the session; a 403 is a plain
/// refusal; a refusal with a message shows that message; anything else is generic (spec §11).</summary>
public sealed class ServerApi(HttpClient http, SessionState session) : IServerApi
{
    public const string Forbidden = "You can't do that.";
    public const string Unexpected = "Something went wrong.";
    public const string NotReachable = "The server can't be reached right now.";

    public Task<ApiResult<MeResponse>> MeAsync() => QueryAsync<MeResponse>(HttpMethod.Get, "api/me", null);

    public Task<ApiResult> LogoutAsync() => CommandAsync(HttpMethod.Post, "logout", null);

    public Task<ApiResult<IReadOnlyList<UserSummary>>> UsersAsync() =>
        QueryAsync<IReadOnlyList<UserSummary>>(HttpMethod.Get, "api/admin/users", null);

    public Task<ApiResult<UserSummary>> CreateUserAsync(CreateUserRequest request) =>
        QueryAsync<UserSummary>(HttpMethod.Post, "api/admin/users", request);

    public Task<ApiResult> SetPasswordAsync(int userId, string password) =>
        CommandAsync(HttpMethod.Put, $"api/admin/users/{userId}/password", new PasswordRequest(password));

    public Task<ApiResult> SetAdminAsync(int userId, bool isAdmin) =>
        CommandAsync(HttpMethod.Put, $"api/admin/users/{userId}/role", new RoleRequest(isAdmin));

    public Task<ApiResult> SetDisabledAsync(int userId, bool disabled) =>
        CommandAsync(HttpMethod.Put, $"api/admin/users/{userId}/disabled", new DisabledRequest(disabled));

    public Task<ApiResult<MaintenanceStatus>> MaintenanceAsync() =>
        QueryAsync<MaintenanceStatus>(HttpMethod.Get, "api/admin/maintenance", null);

    public Task<ApiResult<MaintenanceStatus>> SetMaintenanceAsync(bool on) =>
        QueryAsync<MaintenanceStatus>(HttpMethod.Post, "api/admin/maintenance", new MaintenanceRequest(on));

    private async Task<ApiResult> CommandAsync(HttpMethod method, string path, object? body)
    {
        var (response, error) = await SendAsync(method, path, body);
        response?.Dispose();
        return new ApiResult(error);
    }

    private async Task<ApiResult<T>> QueryAsync<T>(HttpMethod method, string path, object? body)
    {
        var (response, error) = await SendAsync(method, path, body);
        if (response is null) return new ApiResult<T>(default, error);
        using (response)
        {
            try
            {
                return new ApiResult<T>(await response.Content.ReadFromJsonAsync<T>(), null);
            }
            catch (JsonException)
            {
                return new ApiResult<T>(default, Unexpected);
            }
        }
    }

    private async Task<(HttpResponseMessage? Response, string? Error)> SendAsync(HttpMethod method, string path, object? body)
    {
        if (session.IsEnded) return (null, SessionState.Ended);
        using var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            return (null, NotReachable);
        }
        if (response.IsSuccessStatusCode) return (response, null);
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                session.End();
                return (null, SessionState.Ended);
            }
            if (response.StatusCode == HttpStatusCode.Forbidden) return (null, Forbidden);
            return (null, await MessageOfAsync(response) ?? Unexpected);
        }
    }

    private static async Task<string?> MessageOfAsync(HttpResponseMessage response)
    {
        try
        {
            return (await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Error;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
```

`SendAsync` refuses to call once the session has ended. That is what keeps a 401 from turning into a stream of further requests. The `logout` POST has no body, which the server answers with 204.

Create `src/CromoBound.Client/Services/SessionKeeper.cs`:

```csharp
namespace CromoBound.Client.Services;

/// <summary>Renews the session while the app is open (spec §5.1): asks who is signed in every 30 minutes, which slides the cookie.
/// The hub's own traffic doesn't renew it. A ping that finds the session over ends it, through the API.</summary>
public sealed class SessionKeeper(IServerApi api, TimeProvider time) : IAsyncDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private CancellationTokenSource? _stop;

    /// <summary>Starts the pings; a second call does nothing.</summary>
    public void Start()
    {
        if (_stop is not null) return;
        _stop = new CancellationTokenSource();
        _ = RunAsync(_stop.Token);
    }

    public async Task PingAsync() => await api.MeAsync();

    private async Task RunAsync(CancellationToken cancel)
    {
        using var timer = new PeriodicTimer(Interval, time);
        try
        {
            while (await timer.WaitForNextTickAsync(cancel)) await PingAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        _stop?.Cancel();
        _stop?.Dispose();
        return ValueTask.CompletedTask;
    }
}
```

Create `src/CromoBound.Client/CrimsonTheme.cs`:

```csharp
using MudBlazor;

namespace CromoBound.Client;

/// <summary>The approved look for MudBlazor's own components: the Crimson night palette, Fredoka titles and Nunito text, rounded
/// shapes. The app's layout and tiles take the same values from <c>wwwroot/css/app.css</c>.</summary>
public static class CrimsonTheme
{
    private static readonly string[] Body = ["Nunito", "system-ui", "sans-serif"];
    private static readonly string[] Display = ["Fredoka", "Nunito", "sans-serif"];

    public static MudTheme Theme { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#d8392a",
            PrimaryContrastText = "#ffffff",
            Secondary = "#ff8a7a",
            SecondaryContrastText = "#0b0909",
            Error = "#b8284f",
            ErrorContrastText = "#ffffff",
            Info = "#bcd6f5",
            Success = "#4fc3a1",
            Warning = "#e3913f",
            Background = "#0b0909",
            Surface = "#161112",
            AppbarBackground = "#1a1314",
            DrawerBackground = "#161112",
            TextPrimary = "#f2e9e6",
            TextSecondary = "#b39d99",
            TextDisabled = "#7a6764",
            ActionDefault = "#d7c7c3",
            ActionDisabled = "#7a6764",
            ActionDisabledBackground = "#211819",
            LinesDefault = "#3a2a2b",
            LinesInputs = "#3a2a2b",
            Divider = "#2a1f20",
            OverlayDark = "rgba(8,4,4,0.82)",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = Body },
            H1 = new H1Typography { FontFamily = Display },
            H2 = new H2Typography { FontFamily = Display },
            H3 = new H3Typography { FontFamily = Display },
            H4 = new H4Typography { FontFamily = Display },
            H5 = new H5Typography { FontFamily = Display },
            H6 = new H6Typography { FontFamily = Display },
            Button = new ButtonTypography { FontFamily = Body, TextTransform = "none", FontWeight = "600" },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "14px" },
    };
}
```

If MudBlazor 9.11 lacks one of these palette or typography property names, drop that one line and name it in the report. The CSS sets the same values anyway. Don't rename properties by guesswork.

Create `src/CromoBound.Client/wwwroot/css/app.css`:

```css
:root {
  --cb-bg: #0b0909; --cb-bar: #1a1314; --cb-panel: #161112; --cb-raised: #211819; --cb-highlight: #2a1312;
  --cb-edge: #1f1718; --cb-line: #3a2a2b; --cb-text: #f2e9e6; --cb-title: #fff4f1; --cb-muted: #b39d99; --cb-soft: #d7c7c3;
  --cb-label: #a8918d; --cb-primary: #d8392a; --cb-accent: #ff8a7a; --cb-accent-strong: #ff7a6b; --cb-accent-edge: #5a2420;
  --cb-danger: #ff9db0; --cb-danger-fill: #b8284f; --cb-online: #4fc3a1; --cb-online-text: #7fd9bd; --cb-busy: #e3913f;
  --cb-busy-text: #f0b27a; --cb-off: #5e4f4d;
}
html, body { margin: 0; background: var(--cb-bg); color: var(--cb-text); font-family: Nunito, system-ui, sans-serif; }
a { color: var(--cb-accent); }
a:hover { color: #ffb0a5; }
h1, h2, h3 { font-family: Fredoka, Nunito, sans-serif; }

.cb-bar { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 28px; padding: 0 28px; min-height: 64px; margin: 16px 24px 0; border-radius: 999px; background: var(--cb-bar); box-shadow: 0 10px 30px rgba(0,0,0,.45); }
.cb-logo { display: flex; align-items: center; gap: 10px; color: var(--cb-accent-strong); text-decoration: none; font-family: Fredoka, sans-serif; font-weight: 700; font-size: 20px; letter-spacing: .12em; }
.cb-nav { display: flex; flex-wrap: wrap; gap: 4px; flex: 1 1 auto; }
.cb-nav a { color: var(--cb-soft); text-decoration: none; padding: 10px 14px; border-radius: 999px; font-weight: 600; }
.cb-nav a.active { color: var(--cb-accent-strong); background: rgba(224,74,58,.12); }
.cb-me { display: flex; align-items: center; gap: 12px; }
.cb-avatar { width: 34px; height: 34px; border-radius: 50%; background: #2a1716; border: 1px solid #b8463a; color: var(--cb-accent-strong); display: flex; align-items: center; justify-content: center; font-family: Fredoka, sans-serif; font-weight: 700; }

.cb-banner { display: flex; align-items: center; gap: 12px; padding: 12px 24px; margin: 12px 24px 0; border-radius: 999px; font-size: 15px; }
.cb-banner.info { background: #14243a; color: #bcd6f5; border: 1px solid #27466e; }
.cb-banner.warn { background: #2b1712; color: #ffb0a5; border: 1px solid #5a2a18; }

.cb-main { max-width: 1240px; margin: 0 auto; padding: 32px 28px; display: flex; flex-direction: column; gap: 24px; box-sizing: border-box; }
.cb-main.narrow { max-width: 900px; }
.cb-head { display: flex; flex-wrap: wrap; align-items: flex-end; justify-content: space-between; gap: 12px; }
.cb-title { margin: 0; font-family: Fredoka, sans-serif; font-weight: 700; font-size: 36px; letter-spacing: .04em; color: var(--cb-title); }
.cb-sub { margin: 0; color: var(--cb-muted); font-size: 16px; }
.cb-section-title { margin: 0; font-family: Fredoka, sans-serif; font-size: 18px; letter-spacing: .08em; color: var(--cb-accent-strong); text-transform: uppercase; }
.cb-row { display: flex; flex-wrap: wrap; gap: 24px; align-items: flex-start; }
.cb-col-main { flex: 999 1 600px; min-width: 0; display: flex; flex-direction: column; gap: 14px; }
.cb-col-side { flex: 1 1 340px; min-width: 0; display: flex; flex-direction: column; gap: 14px; }
.cb-panel { background: var(--cb-panel); border: 1px solid var(--cb-edge); border-radius: 24px; padding: 16px; display: flex; flex-direction: column; gap: 14px; }
.cb-panel.highlight { background: var(--cb-highlight); border-color: var(--cb-accent-edge); }
.cb-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); gap: 14px; }
.cb-kicker { font-size: 12px; font-weight: 600; letter-spacing: .12em; text-transform: uppercase; color: var(--cb-muted); }
.cb-kicker.accent { color: var(--cb-accent-strong); }
.cb-chip { display: inline-flex; align-items: center; gap: 6px; padding: 2px 10px; border-radius: 999px; font-size: 13px; border: 1px solid var(--cb-accent-edge); }
.cb-actions { display: flex; flex-wrap: wrap; gap: 8px; justify-content: flex-end; }

.cb-player-head { display: flex; align-items: center; gap: 12px; }
.cb-avatar-lg { width: 46px; height: 46px; border-radius: 50%; background: var(--cb-raised); border: 2px solid var(--cb-off); display: flex; align-items: center; justify-content: center; font-family: Fredoka, sans-serif; font-weight: 700; font-size: 18px; }
.cb-player-name { display: block; font-size: 17px; font-weight: 600; }
.cb-status { display: flex; align-items: center; gap: 6px; font-size: 13px; }
.cb-dot { width: 8px; height: 8px; border-radius: 50%; }

.cb-field { display: flex; flex-direction: column; gap: 8px; }
.cb-label { font-size: 13px; letter-spacing: .1em; color: var(--cb-label); font-weight: 600; text-transform: uppercase; }
.cb-input { min-height: 48px; padding: 0 12px; border: 1px solid var(--cb-line); border-radius: 14px; background: #110c0d; color: var(--cb-text); font: inherit; font-size: 15px; box-sizing: border-box; width: 100%; }
textarea.cb-input { padding: 12px; min-height: 200px; font-family: ui-monospace, monospace; font-size: 13px; resize: vertical; }
.cb-input.invalid { border: 2px solid #e0507a; }
.cb-error { margin: 0; font-size: 14px; color: var(--cb-danger); }
.cb-choices { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: 10px; }
.cb-choice { display: flex; align-items: center; gap: 10px; min-height: 52px; padding: 0 14px; border-radius: 16px; border: 1px solid var(--cb-line); cursor: pointer; }
.cb-choice.on { border-color: var(--cb-primary); background: rgba(224,74,58,.1); }
.cb-choice input, .cb-check input { width: 18px; height: 18px; accent-color: var(--cb-primary); }
.cb-check { display: flex; align-items: center; gap: 10px; min-height: 44px; cursor: pointer; font-size: 15px; }

.cb-deck-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: 20px; }
.cb-deckbox { position: relative; height: 230px; }
.cb-deckbox .back, .cb-deckbox .mid { position: absolute; border-radius: 24px; }
.cb-deckbox .back { inset: 14px 0 0 14px; background: #171112; border: 1px solid #2a2021; }
.cb-deckbox .mid { inset: 7px; background: #1a1314; border: 1px solid #2f2324; }
.cb-deckbox .front { position: absolute; inset: 0 14px 14px 0; border-radius: 24px; background: var(--cb-raised); border: 1px solid var(--cb-line); display: flex; flex-direction: column; justify-content: flex-end; overflow: hidden; }
.cb-deckbox .label { padding: 12px 14px; background: rgba(14,9,9,.82); display: flex; flex-direction: column; gap: 4px; }
.cb-deck-name { font-family: Fredoka, sans-serif; font-weight: 700; font-size: 17px; }
.cb-deck-counts { font-size: 14px; color: var(--cb-soft); }

.cb-score { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); align-items: center; gap: 16px; }
.cb-score div { display: flex; flex-direction: column; align-items: center; gap: 4px; }
.cb-score b { font-weight: 700; font-size: 72px; line-height: 1; }
.cb-placeholder { border: 1px dashed var(--cb-line); border-radius: 20px; padding: 28px 24px; text-align: center; color: var(--cb-muted); font-size: 16px; }

.cb-tabs { display: flex; gap: 4px; border-bottom: 1px solid var(--cb-edge); }
.cb-tabs a { padding: 12px 16px; color: var(--cb-soft); text-decoration: none; border-bottom: 2px solid transparent; }
.cb-tabs a.active { color: var(--cb-accent-strong); border-bottom-color: var(--cb-primary); font-weight: 600; }
.cb-table { width: 100%; border-collapse: collapse; font-size: 15px; }
.cb-table th { text-align: left; padding: 12px 20px; color: var(--cb-label); font-size: 13px; letter-spacing: .08em; font-weight: 600; border-bottom: 1px solid var(--cb-edge); }
.cb-table td { padding: 10px 20px; border-bottom: 1px solid var(--cb-raised); }
.cb-big { font-weight: 700; font-size: 64px; line-height: 1; color: var(--cb-accent-strong); }

.mud-button-root { border-radius: 999px !important; min-height: 44px; }
.mud-dialog { border-radius: 28px !important; background: var(--cb-panel) !important; border: 1px solid var(--cb-accent-edge); }
.mud-dialog .mud-dialog-title { font-family: Fredoka, sans-serif; font-size: 22px; letter-spacing: .04em; }
.cb-danger-text { color: var(--cb-danger) !important; }
```

Replace `src/CromoBound.Client/wwwroot/index.html` with:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="robots" content="noindex, nofollow" />
    <title>Play</title>
    <base href="/" />
    <link rel="preconnect" href="https://fonts.googleapis.com" />
    <link rel="stylesheet" href="https://fonts.googleapis.com/css2?family=Fredoka:wght@500;600;700&amp;family=Nunito:wght@400;600;700&amp;display=swap" />
    <link rel="stylesheet" href="_content/MudBlazor/MudBlazor.min.css" />
    <link rel="stylesheet" href="css/app.css" />
    <style>
        #blazor-error-ui { display: none; }
    </style>
</head>
<body>
    <div id="app"></div>
    <div id="blazor-error-ui">Something went wrong. <a href="" class="reload">Reload the page.</a></div>
    <script src="_framework/blazor.webassembly.js"></script>
    <script src="_content/MudBlazor/MudBlazor.min.js"></script>
</body>
</html>
```

Replace `src/CromoBound.Client/App.razor` with:

```razor
<Router AppAssembly="typeof(App).Assembly">
    <Found Context="route">
        <RouteView RouteData="route" DefaultLayout="typeof(MainLayout)" />
        <FocusOnNavigate RouteData="route" Selector="h1" />
    </Found>
    <NotFound>
        <LayoutView Layout="typeof(MainLayout)">
            <main class="cb-main"><p class="cb-sub">There's nothing here. <a href="">Back to the lobby</a></p></main>
        </LayoutView>
    </NotFound>
</Router>
```

Create `src/CromoBound.Client/Layout/MainLayout.razor`:

```razor
@inherits LayoutComponentBase
@implements IDisposable
@inject SessionState Session
@inject IServerApi Api
@inject SessionKeeper Keeper
@inject NavigationManager Nav

<MudThemeProvider Theme="CrimsonTheme.Theme" IsDarkMode="true" />
<MudPopoverProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<header class="cb-bar">
    <a class="cb-logo" href="">
        <svg aria-hidden="true" width="26" height="26" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6"><path d="M12 2l9 10-9 10-9-10z"></path><path d="M12 7l4.5 5-4.5 5-4.5-5z"></path></svg>
        CROMOBOUND
    </a>
    <nav class="cb-nav" aria-label="Main">
        <NavLink href="" Match="NavLinkMatch.All">Play</NavLink>
        <NavLink href="decks">Decks</NavLink>
        @if (Session.Me?.CanManageUsers == true)
        {
            <NavLink href="admin/users">Admin</NavLink>
        }
    </nav>
    <div class="cb-me">
        @if (Session.Me is { } me)
        {
            <span class="cb-avatar" aria-hidden="true">@me.UserName[..1].ToUpperInvariant()</span>
            <span>@me.UserName</span>
        }
        <MudButton id="sign-out" Variant="Variant.Outlined" Color="Color.Secondary" OnClick="SignOutAsync">Sign out</MudButton>
    </div>
</header>

@if (failure is not null)
{
    <main class="cb-main"><p class="cb-error" role="alert">@failure</p></main>
}
else if (ready)
{
    @Body
}

@code {
    private bool ready;
    private string? failure;

    /// <summary>Asks who is signed in before showing any page: a 401 goes to the login page, any other failure says so.</summary>
    protected override async Task OnInitializedAsync()
    {
        Session.SessionEnded += GoToLogin;
        if (Session.IsEnded)
        {
            GoToLogin();
            return;
        }
        var me = await Api.MeAsync();
        if (me.Value is null)
        {
            if (!Session.IsEnded) failure = me.Error;
            return;
        }
        Session.SignedIn(me.Value);
        Keeper.Start();
        await OnSignedInAsync();
        ready = true;
    }

    /// <summary>What else starts once the player is known (Task 3 connects the hub here).</summary>
    private Task OnSignedInAsync() => Task.CompletedTask;

    private void GoToLogin() => Nav.NavigateTo("/login", forceLoad: true);

    private async Task SignOutAsync()
    {
        await Api.LogoutAsync();
        GoToLogin();
    }

    public void Dispose() => Session.SessionEnded -= GoToLogin;
}
```

Create `src/CromoBound.Client/Pages/Lobby.razor` (a placeholder until Task 5):

```razor
@page "/"

<main class="cb-main">
    <h1 class="cb-title">Play</h1>
</main>
```

Replace `src/CromoBound.Client/Program.cs` with:

```csharp
using CromoBound.Client;
using CromoBound.Client.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddMudServices();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SessionState>();
builder.Services.AddScoped<IServerApi, ServerApi>();
builder.Services.AddScoped<SessionKeeper>();

await builder.Build().RunAsync();
```

In WebAssembly, scoped and singleton services both last as long as the tab.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, the new `CromoBound.Client.Tests` included).

- [ ] **Step 6: Commit**

```bash
git add src/CromoBound.Client tests/CromoBound.Client.Tests CromoBound.slnx
git commit -m "feat(client): add the theme, the server api, session renewal and the top bar"
```

---
### Task 3: The hub connection and the lobby's state

**Files:**
- Create:
  - `src/CromoBound.Client/Formats.cs`
  - `src/CromoBound.Client/Services/IGameHub.cs`, `Services/GameConnection.cs`, `Services/ForeverRetryPolicy.cs`, `Services/LobbyState.cs`, `Services/LobbySync.cs`
  - `tests/CromoBound.Client.Tests/Fakes/FakeGameHub.cs`
  - `tests/CromoBound.Client.Tests/LobbyStateTests.cs`, `LobbySyncTests.cs`, `RetryPolicyTests.cs`
- Modify: `src/CromoBound.Client/Program.cs`, `Layout/MainLayout.razor`, `tests/CromoBound.Client.Tests/Ui.cs`, `tests/CromoBound.Client.Tests/LayoutTests.cs`

**Interfaces:**
- Consumes:
  - Task 2's `SessionState`, `IServerApi`, `FakeServerApi`, `Ui`.
  - The contracts' `IGameClient` and its notices, `LobbyReply`, `MatchReply` (with Task 1's `Opponent`), `HubReply`, `SubmitReply`, `ChallengeInfo`, `PlayerPresence`.
  - The hub at `/hub`, with the methods `GetLobby`, `GetMatch`, `Challenge`, `AcceptChallenge`, `DeclineChallenge`, `CancelChallenge` and `Submit`. It speaks `WireJson.Options`.
- Produces:
  - `HubState` {`Connecting`, `Connected`, `Reconnecting`}.
  - `IGameHub`:
    - `State`, the events `StateChanged`, `Connected` (`Func<Task>`) and `Closed` (`Func<Task>`);
    - `Listen(IGameClient)`, `StartAsync()`;
    - `GetLobbyAsync()` returns `LobbyReply?`, and `GetMatchAsync()` returns `MatchReply?` (null when the call failed);
    - `ChallengeAsync(string, MatchFormat, Deck)`, `AcceptAsync(Guid, Deck)`, `DeclineAsync(Guid)`, `CancelAsync(Guid)` (all `HubReply`);
    - `SubmitAsync(Guid, PlayerAction)` (`SubmitReply`).
  - `GameConnection` (the real one), with the constant `Unexpected`; `ForeverRetryPolicy`.
  - `LobbyState : IGameClient`:
    - `Loaded`, `Players`, `Online`, `Received`, `Sent`;
    - `MatchId`, `Opponent`, `Seat`, `CurrentView`, `Ended`, `Maintenance`, `Connection`, `IsConnected`;
    - `Load(LobbyReply, MatchReply?)`, `SetConnection(HubState)`, `ChallengeSent(ChallengeInfo)`, `ChallengeGone(Guid)`, `TakeAbandoned()`;
    - the events `Changed`, `PlayersChanged`, `Notice` (`Action<string>`) and `MatchBegun` (`Action<Guid>`).
  - `LobbySync`: `StartAsync()`, `ReloadAsync()`.
  - `Formats`: `Name(MatchFormat)`, `InSentence(MatchFormat)`, `Stage(MatchStage)`.
  - In the tests:
    - `FakeGameHub`, with `Calls`, `Replies` and `Push`;
    - `Ui.Hub`, `Ui.Lobby`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Client.Tests/Fakes/FakeGameHub.cs`:

```csharp
using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Tests.Fakes;

/// <summary>The hub, in memory. A test sets what each call answers, pushes notices with <see cref="Push"/>, and reads the calls made.</summary>
internal sealed class FakeGameHub : IGameHub
{
    private readonly List<IGameClient> _listeners = [];

    public HubState State { get; private set; } = HubState.Connecting;
    public event Action? StateChanged;
    public event Func<Task>? Connected;
    public event Func<Task>? Closed;

    public List<string> Calls { get; } = [];
    public int Starts { get; private set; }

    public LobbyReply? Lobby { get; set; } = new([], [], null, null, false);
    public MatchReply? Match { get; set; } = MatchReply.None;

    /// <summary>What the next challenge call answers, by method name ("Challenge", "Accept", "Decline", "Cancel"); Ok by default.</summary>
    public Dictionary<string, HubReply> Replies { get; } = [];

    public SubmitReply Submitted { get; set; } = new(true, null, null);
    public List<PlayerAction> Actions { get; } = [];
    public List<Deck> Decks { get; } = [];

    public void Listen(IGameClient listener) => _listeners.Add(listener);

    public Task StartAsync()
    {
        Starts++;
        return Task.CompletedTask;
    }

    /// <summary>Moves to a state, firing <see cref="Connected"/> when it becomes connected, as the real connection does.</summary>
    public async Task SetStateAsync(HubState state)
    {
        State = state;
        StateChanged?.Invoke();
        if (state == HubState.Connected && Connected is { } connected) await connected();
    }

    public async Task CloseAsync()
    {
        State = HubState.Reconnecting;
        StateChanged?.Invoke();
        if (Closed is { } closed) await closed();
    }

    public Task Push(Func<IGameClient, Task> notice) => Task.WhenAll(_listeners.Select(notice));

    public Task<LobbyReply?> GetLobbyAsync()
    {
        Calls.Add("GetLobby");
        return Task.FromResult(Lobby);
    }

    public Task<MatchReply?> GetMatchAsync()
    {
        Calls.Add("GetMatch");
        return Task.FromResult(Match);
    }

    public Task<HubReply> ChallengeAsync(string opponent, MatchFormat format, Deck deck)
    {
        Calls.Add($"Challenge {opponent} {format} {deck.Name}");
        Decks.Add(deck);
        return Task.FromResult(Reply("Challenge"));
    }

    public Task<HubReply> AcceptAsync(Guid challengeId, Deck deck)
    {
        Calls.Add($"Accept {challengeId} {deck.Name}");
        Decks.Add(deck);
        return Task.FromResult(Reply("Accept"));
    }

    public Task<HubReply> DeclineAsync(Guid challengeId)
    {
        Calls.Add($"Decline {challengeId}");
        return Task.FromResult(Reply("Decline"));
    }

    public Task<HubReply> CancelAsync(Guid challengeId)
    {
        Calls.Add($"Cancel {challengeId}");
        return Task.FromResult(Reply("Cancel"));
    }

    public Task<SubmitReply> SubmitAsync(Guid matchId, PlayerAction action)
    {
        Calls.Add($"Submit {matchId} {action.GetType().Name}");
        Actions.Add(action);
        return Task.FromResult(Submitted);
    }

    private HubReply Reply(string method) => Replies.Remove(method, out var reply) ? reply : HubReply.Ok(Guid.NewGuid());
}
```

In `tests/CromoBound.Client.Tests/Ui.cs`, add the hub and the lobby state. Add the properties:

```csharp
    public FakeGameHub Hub { get; } = new();
    public LobbyState Lobby { get; }
```

and the notifications shown so far, read from MudBlazor's snackbar service (with `using MudBlazor;` at the top):

```csharp
    public IReadOnlyList<string> Notices =>
        [.. Ctx.Services.GetRequiredService<ISnackbar>().ShownSnackbars.Select(s => s.Message ?? "")];
```

and in the constructor, after the `SessionKeeper` registration:

```csharp
        Lobby = new LobbyState(Session);
        Ctx.Services.AddSingleton<IGameHub>(Hub);
        Ctx.Services.AddSingleton(Lobby);
        Ctx.Services.AddSingleton<LobbySync>();
```

Create `tests/CromoBound.Client.Tests/LobbyStateTests.cs`:

```csharp
using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;

namespace CromoBound.Client.Tests;

public class LobbyStateTests
{
    private static readonly Guid A = Guid.Parse("00000000-0000-0000-0000-00000000000a");
    private static readonly Guid B = Guid.Parse("00000000-0000-0000-0000-00000000000b");
    private static readonly Guid C = Guid.Parse("00000000-0000-0000-0000-00000000000c");
    private static readonly Guid M = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    private static (LobbyState State, List<string> Notices) Marco()
    {
        var session = new SessionState();
        session.SignedIn(new MeResponse("marco", false));
        var state = new LobbyState(session);
        var notices = new List<string>();
        state.Notice += notices.Add;
        return (state, notices);
    }

    private static LobbyReply Reply(
        IReadOnlyList<PlayerPresence>? players = null, IReadOnlyList<ChallengeInfo>? challenges = null, Guid? match = null,
        MatchEndedNotice? ended = null, bool maintenance = false) =>
        new(players ?? [], challenges ?? [], match, ended, maintenance);

    [Fact]
    public void Loading_sorts_players_and_splits_the_challenges()
    {
        var (state, _) = Marco();

        state.Load(Reply(
            [new("sara", true, true), new("Giulia", true, false), new("luca", false, false)],
            [new(B, "marco", "luca", MatchFormat.Bo1), new(C, "sara", "marco", MatchFormat.Bo1), new(A, "giulia", "marco", MatchFormat.Bo3)]), null);

        Assert.True(state.Loaded);
        Assert.Equal(new[] { "Giulia", "luca", "sara" }, state.Players.Select(p => p.UserName));
        Assert.Equal(2, state.Online);
        Assert.Equal(new[] { "giulia", "sara" }, state.Received.Select(c => c.From));
        Assert.Equal(B, state.Sent?.ChallengeId);
    }

    [Fact]
    public void Loading_replaces_everything_and_keeps_the_match_reply()
    {
        var (state, _) = Marco();
        state.Load(Reply([new("giulia", true, false)], [new(A, "giulia", "marco", MatchFormat.Bo3)]), null);

        state.Load(Reply(match: M, maintenance: true), new MatchReply(M, null, "giulia"));

        Assert.Empty(state.Players);
        Assert.Empty(state.Received);
        Assert.Equal((M, "giulia", true), (state.MatchId, state.Opponent, state.Maintenance));
    }

    [Fact]
    public void An_abandoned_notice_is_taken_once()
    {
        var (state, _) = Marco();
        var ended = new MatchEndedNotice(M, MatchEndReason.Abandoned, [0, 0], null);

        state.Load(Reply(ended: ended), null);

        Assert.Equal(ended, state.TakeAbandoned());
        Assert.Null(state.TakeAbandoned());
    }

    [Fact]
    public async Task A_received_challenge_is_listed_and_announced()
    {
        var (state, notices) = Marco();
        state.Load(Reply(), null);

        await state.ChallengeReceived(new ChallengeNotice(A, "giulia", MatchFormat.Bo3));
        await state.ChallengeReceived(new ChallengeNotice(A, "giulia", MatchFormat.Bo3));

        Assert.Equal(new ChallengeInfo(A, "giulia", "marco", MatchFormat.Bo3), Assert.Single(state.Received));
        Assert.Equal(new[] { "giulia challenges you to a best of three." }, notices);
    }

    [Theory]
    [InlineData(ChallengeEnd.Declined, "luca declined your challenge.")]
    [InlineData(ChallengeEnd.Withdrawn, "Your challenge to luca was withdrawn.")]
    [InlineData(ChallengeEnd.Cancelled, "")]
    [InlineData(ChallengeEnd.Accepted, "")]
    public async Task Closing_your_sent_challenge_says_why(ChallengeEnd reason, string notice)
    {
        var (state, notices) = Marco();
        state.Load(Reply(challenges: [new(B, "marco", "luca", MatchFormat.Bo1)]), null);

        await state.ChallengeClosed(new ChallengeClosedNotice(B, reason));

        Assert.Null(state.Sent);
        Assert.Equal(notice, string.Join("|", notices));
    }

    [Theory]
    [InlineData(ChallengeEnd.Cancelled, "giulia cancelled their challenge.")]
    [InlineData(ChallengeEnd.Withdrawn, "giulia's challenge was withdrawn.")]
    [InlineData(ChallengeEnd.Declined, "")]
    [InlineData(ChallengeEnd.Accepted, "")]
    public async Task Closing_a_received_challenge_says_why(ChallengeEnd reason, string notice)
    {
        var (state, notices) = Marco();
        state.Load(Reply(challenges: [new(A, "giulia", "marco", MatchFormat.Bo3)]), null);

        await state.ChallengeClosed(new ChallengeClosedNotice(A, reason));

        Assert.Empty(state.Received);
        Assert.Equal(notice, string.Join("|", notices));
    }

    [Fact]
    public async Task Closing_an_unknown_challenge_changes_nothing()
    {
        var (state, notices) = Marco();
        state.Load(Reply(), null);
        var changes = 0;
        state.Changed += () => changes++;

        await state.ChallengeClosed(new ChallengeClosedNotice(C, ChallengeEnd.Declined));

        Assert.Equal((0, 0), (changes, notices.Count));
    }

    [Fact]
    public async Task A_started_match_is_tracked_and_announced_to_the_app()
    {
        var (state, _) = Marco();
        state.Load(Reply(), null);
        Guid? begun = null;
        state.MatchBegun += id => begun = id;

        await state.MatchStarted(new MatchStartedNotice(M, "giulia", new PlayerId(1)));

        Assert.Equal((M, "giulia", new PlayerId(1)), (state.MatchId, state.Opponent, state.Seat));
        Assert.Equal(M, begun);
    }

    [Fact]
    public async Task A_finished_match_is_kept_for_the_match_page_and_frees_the_player()
    {
        var (state, _) = Marco();
        state.Load(Reply(), null);
        await state.MatchStarted(new MatchStartedNotice(M, "giulia", new PlayerId(0)));
        var ended = new MatchEndedNotice(M, MatchEndReason.Finished, [2, 1], "marco");

        await state.MatchEnded(ended);

        Assert.Null(state.MatchId);
        Assert.Equal(ended, state.Ended);
    }

    [Fact]
    public async Task Presence_and_maintenance_notices_update_the_lobby()
    {
        var (state, _) = Marco();
        state.Load(Reply([new("luca", false, false), new("sara", true, false)]), null);
        var playerChanges = 0;
        state.PlayersChanged += () => playerChanges++;

        await state.PlayerChanged(new PlayerPresence("luca", true, false));
        await state.PlayerChanged(new PlayerPresence("chiara", true, false));
        await state.PlayerLeft(new PlayerLeftNotice("sara"));
        await state.MaintenanceChanged(new MaintenanceNotice(true));

        Assert.Equal(new[] { ("chiara", true), ("luca", true) }, state.Players.Select(p => (p.UserName, p.Online)));
        Assert.True(state.Maintenance);
        Assert.Equal(4, playerChanges);
    }

    [Fact]
    public void A_challenge_sent_from_this_tab_is_shown_until_it_is_gone()
    {
        var (state, _) = Marco();
        state.Load(Reply(), null);

        state.ChallengeSent(new ChallengeInfo(B, "marco", "luca", MatchFormat.Bo1));
        Assert.Equal(B, state.Sent?.ChallengeId);

        state.ChallengeGone(B);
        Assert.Null(state.Sent);
    }
}
```

Create `tests/CromoBound.Client.Tests/LobbySyncTests.cs`:

```csharp
using CromoBound.Client.Services;
using CromoBound.Client.Tests.Fakes;
using CromoBound.Contracts;

namespace CromoBound.Client.Tests;

public class LobbySyncTests
{
    private static (LobbySync Sync, FakeGameHub Hub, LobbyState State, FakeServerApi Api, SessionState Session) Sync()
    {
        var session = new SessionState();
        session.SignedIn(new MeResponse("marco", false));
        var hub = new FakeGameHub();
        var state = new LobbyState(session);
        var api = new FakeServerApi(session);
        return (new LobbySync(hub, state, api, session), hub, state, api, session);
    }

    [Fact]
    public async Task Every_connect_reloads_the_lobby()
    {
        var (sync, hub, state, _, _) = Sync();
        await sync.StartAsync();
        hub.Lobby = new LobbyReply([new("giulia", true, false)], [], null, null, false);

        await hub.SetStateAsync(HubState.Connected);
        await hub.SetStateAsync(HubState.Reconnecting);
        Assert.Equal(HubState.Reconnecting, state.Connection);
        await hub.SetStateAsync(HubState.Connected);

        Assert.Equal(1, hub.Starts);
        Assert.Equal(new[] { "GetLobby", "GetLobby" }, hub.Calls);
        Assert.Equal("giulia", Assert.Single(state.Players).UserName);
        Assert.Equal(HubState.Connected, state.Connection);
    }

    [Fact]
    public async Task A_running_match_is_fetched_with_the_lobby()
    {
        var (sync, hub, state, _, _) = Sync();
        var match = Guid.NewGuid();
        hub.Lobby = new LobbyReply([], [], match, null, false);
        hub.Match = new MatchReply(match, null, "giulia");
        await sync.StartAsync();

        await hub.SetStateAsync(HubState.Connected);

        Assert.Equal(new[] { "GetLobby", "GetMatch" }, hub.Calls);
        Assert.Equal((match, "giulia"), (state.MatchId, state.Opponent));
    }

    [Fact]
    public async Task Notices_reach_the_lobby_state()
    {
        var (sync, hub, state, _, _) = Sync();
        await sync.StartAsync();
        await hub.SetStateAsync(HubState.Connected);

        await hub.Push(c => c.MaintenanceChanged(new MaintenanceNotice(true)));

        Assert.True(state.Maintenance);
    }

    [Fact]
    public async Task A_closed_connection_checks_the_session_then_starts_again()
    {
        var (sync, hub, _, api, _) = Sync();
        await sync.StartAsync();

        await hub.CloseAsync();

        Assert.Equal(1, api.MeCalls);
        Assert.Equal(2, hub.Starts);
    }

    [Fact]
    public async Task A_closed_connection_with_the_session_over_stays_closed()
    {
        var (sync, hub, _, api, session) = Sync();
        await sync.StartAsync();
        api.SessionOver = true;

        await hub.CloseAsync();

        Assert.True(session.IsEnded);
        Assert.Equal(1, hub.Starts);
    }
}
```

Create `tests/CromoBound.Client.Tests/RetryPolicyTests.cs`:

```csharp
using CromoBound.Client.Services;
using Microsoft.AspNetCore.SignalR.Client;

namespace CromoBound.Client.Tests;

public class RetryPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 5)]
    [InlineData(3, 10)]
    [InlineData(4, 30)]
    [InlineData(500, 30)]
    public void Retries_wait_0_2_5_10_then_30_seconds_forever(long attempt, int seconds)
    {
        var delay = new ForeverRetryPolicy().NextRetryDelay(new RetryContext { PreviousRetryCount = attempt });

        Assert.Equal(TimeSpan.FromSeconds(seconds), delay);
    }
}
```

Add to `tests/CromoBound.Client.Tests/LayoutTests.cs`:

```csharp
    [Fact]
    public async Task The_layout_starts_the_hub_and_shows_the_banners()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));
        cut.WaitForAssertion(() => Assert.Equal(1, ui.Hub.Starts));

        await cut.InvokeAsync(() => ui.Hub.SetStateAsync(HubState.Reconnecting));
        cut.WaitForAssertion(() => Assert.Contains("Reconnecting...", cut.Find(".cb-banner.warn").TextContent));

        await cut.InvokeAsync(() => ui.Hub.SetStateAsync(HubState.Connected));
        await cut.InvokeAsync(() => ui.Hub.Push(c => c.MaintenanceChanged(new MaintenanceNotice(true))));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".cb-banner.warn")));
        Assert.Contains("The server is in maintenance.", cut.Find(".cb-banner.info").TextContent);
    }

    [Fact]
    public async Task A_notice_pops_up_as_a_notification()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));
        cut.WaitForAssertion(() => Assert.Equal(1, ui.Hub.Starts));
        await cut.InvokeAsync(() => ui.Hub.SetStateAsync(HubState.Connected));

        await cut.InvokeAsync(() => ui.Hub.Push(c => c.ChallengeReceived(new ChallengeNotice(Guid.NewGuid(), "giulia", MatchFormat.Bo3))));

        cut.WaitForAssertion(() => Assert.Contains("giulia challenges you to a best of three.", ui.Notices));
    }

    [Fact]
    public async Task A_started_match_opens_its_page_from_anywhere()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));
        cut.WaitForAssertion(() => Assert.Equal(1, ui.Hub.Starts));
        await cut.InvokeAsync(() => ui.Hub.SetStateAsync(HubState.Connected));
        var match = Guid.NewGuid();

        await cut.InvokeAsync(() => ui.Hub.Push(c => c.MatchStarted(new MatchStartedNotice(match, "giulia", new PlayerId(0)))));

        cut.WaitForAssertion(() => Assert.EndsWith($"/match/{match}", ui.Nav.Uri));
    }
```

with these usings added at the top of the file:

```csharp
using CromoBound.Client.Services;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`IGameHub`, `HubState`, `LobbyState`, `LobbySync` and `ForeverRetryPolicy` don't exist).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Client/Formats.cs`:

```csharp
using CromoBound.Engine.Matches;

namespace CromoBound.Client;

/// <summary>How formats and match stages read in the app.</summary>
public static class Formats
{
    public static string Name(MatchFormat format) => format == MatchFormat.Bo3 ? "Best of three" : "Best of one";

    /// <summary>For the middle of a sentence: "a best of three".</summary>
    public static string InSentence(MatchFormat format) => format == MatchFormat.Bo3 ? "a best of three" : "a best of one";

    public static string Stage(MatchStage stage) => stage switch
    {
        MatchStage.PickBattlefields => "Picking battlefields",
        MatchStage.PlayOrder => "Choosing who goes first",
        MatchStage.Sideboarding => "Sideboarding",
        MatchStage.Mulligan => "Mulligan",
        MatchStage.Playing => "Playing",
        _ => "Over",
    };
}
```

Create `src/CromoBound.Client/Services/IGameHub.cs`:

```csharp
using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Services;

public enum HubState { Connecting, Connected, Reconnecting }

/// <summary>The tab's one hub connection (spec §5.2). Notices go to every listener; calls never throw: a failed call answers with the
/// generic error, or null for the two queries.</summary>
public interface IGameHub
{
    HubState State { get; }

    event Action? StateChanged;

    /// <summary>After every connect and reconnect.</summary>
    event Func<Task>? Connected;

    /// <summary>The connection is closed for good: the server closed it, or reconnecting stopped.</summary>
    event Func<Task>? Closed;

    void Listen(IGameClient listener);

    /// <summary>Connects, trying again with the reconnect delays until it does.</summary>
    Task StartAsync();

    Task<LobbyReply?> GetLobbyAsync();
    Task<MatchReply?> GetMatchAsync();
    Task<HubReply> ChallengeAsync(string opponent, MatchFormat format, Deck deck);
    Task<HubReply> AcceptAsync(Guid challengeId, Deck deck);
    Task<HubReply> DeclineAsync(Guid challengeId);
    Task<HubReply> CancelAsync(Guid challengeId);
    Task<SubmitReply> SubmitAsync(Guid matchId, PlayerAction action);
}
```

Create `src/CromoBound.Client/Services/ForeverRetryPolicy.cs`:

```csharp
using Microsoft.AspNetCore.SignalR.Client;

namespace CromoBound.Client.Services;

/// <summary>Reconnect after 0, 2, 5 and 10 seconds, then every 30 seconds for as long as the tab is open (spec §5.2).</summary>
public sealed class ForeverRetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan[] First = [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];
    private static readonly TimeSpan Then = TimeSpan.FromSeconds(30);

    public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
        retryContext.PreviousRetryCount < First.Length ? First[retryContext.PreviousRetryCount] : Then;
}
```

Create `src/CromoBound.Client/Services/GameConnection.cs`:

```csharp
using System.Text.Json;
using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Client.Services;

/// <summary>The real hub connection, over the app's own origin so the session cookie goes along, in the hub's wire JSON.</summary>
public sealed class GameConnection : IGameHub, IAsyncDisposable
{
    public const string Unexpected = "Something went wrong.";

    private readonly HubConnection _connection;
    private readonly ForeverRetryPolicy _retry = new();
    private readonly List<IGameClient> _listeners = [];
    private readonly CancellationTokenSource _stop = new();

    public GameConnection(NavigationManager nav)
    {
        _connection = new HubConnectionBuilder()
            .WithUrl(nav.ToAbsoluteUri("hub"))
            .WithAutomaticReconnect(_retry)
            .AddJsonProtocol(json => json.PayloadSerializerOptions = WireJson.Options)
            .Build();
        _connection.Reconnecting += _ => SetState(HubState.Reconnecting);
        _connection.Reconnected += _ => ConnectedAsync();
        _connection.Closed += _ => ClosedAsync();
        On<ChallengeNotice>(nameof(IGameClient.ChallengeReceived), (l, n) => l.ChallengeReceived(n));
        On<ChallengeClosedNotice>(nameof(IGameClient.ChallengeClosed), (l, n) => l.ChallengeClosed(n));
        On<MatchStartedNotice>(nameof(IGameClient.MatchStarted), (l, n) => l.MatchStarted(n));
        On<MatchViewNotice>(nameof(IGameClient.View), (l, n) => l.View(n));
        On<MatchEndedNotice>(nameof(IGameClient.MatchEnded), (l, n) => l.MatchEnded(n));
        On<PlayerPresence>(nameof(IGameClient.PlayerChanged), (l, n) => l.PlayerChanged(n));
        On<PlayerLeftNotice>(nameof(IGameClient.PlayerLeft), (l, n) => l.PlayerLeft(n));
        On<MaintenanceNotice>(nameof(IGameClient.MaintenanceChanged), (l, n) => l.MaintenanceChanged(n));
    }

    public HubState State { get; private set; } = HubState.Connecting;

    public event Action? StateChanged;
    public event Func<Task>? Connected;
    public event Func<Task>? Closed;

    public void Listen(IGameClient listener) => _listeners.Add(listener);

    public async Task StartAsync()
    {
        for (var attempt = 0L; !_stop.IsCancellationRequested; attempt++)
        {
            try
            {
                await _connection.StartAsync(_stop.Token);
                await ConnectedAsync();
                return;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !_stop.IsCancellationRequested)
            {
                await SetState(HubState.Reconnecting);
                var delay = _retry.NextRetryDelay(new RetryContext { PreviousRetryCount = attempt, RetryReason = ex }) ?? TimeSpan.FromSeconds(30);
                try
                {
                    await Task.Delay(delay, _stop.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }
    }

    public Task<LobbyReply?> GetLobbyAsync() => QueryAsync<LobbyReply>("GetLobby");

    public Task<MatchReply?> GetMatchAsync() => QueryAsync<MatchReply>("GetMatch");

    public Task<HubReply> ChallengeAsync(string opponent, MatchFormat format, Deck deck) => ReplyAsync("Challenge", opponent, format, deck);

    public Task<HubReply> AcceptAsync(Guid challengeId, Deck deck) => ReplyAsync("AcceptChallenge", challengeId, deck);

    public Task<HubReply> DeclineAsync(Guid challengeId) => ReplyAsync("DeclineChallenge", challengeId);

    public Task<HubReply> CancelAsync(Guid challengeId) => ReplyAsync("CancelChallenge", challengeId);

    public async Task<SubmitReply> SubmitAsync(Guid matchId, PlayerAction action)
    {
        try
        {
            var payload = JsonSerializer.SerializeToElement<PlayerAction>(action, WireJson.Options);
            return await _connection.InvokeAsync<SubmitReply>("Submit", matchId, payload);
        }
        catch (Exception)
        {
            return new SubmitReply(false, null, Unexpected);
        }
    }

    private void On<T>(string method, Func<IGameClient, T, Task> deliver) =>
        _connection.On<T>(method, notice => Task.WhenAll(_listeners.Select(l => deliver(l, notice))));

    private async Task<T?> QueryAsync<T>(string method) where T : class
    {
        try
        {
            return await _connection.InvokeAsync<T>(method);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task<HubReply> ReplyAsync(string method, params object?[] args)
    {
        try
        {
            return await _connection.InvokeCoreAsync<HubReply>(method, args);
        }
        catch (Exception)
        {
            return HubReply.Fail(Unexpected);
        }
    }

    private async Task ConnectedAsync()
    {
        await SetState(HubState.Connected);
        if (Connected is { } connected) await connected();
    }

    private async Task ClosedAsync()
    {
        await SetState(HubState.Reconnecting);
        if (!_stop.IsCancellationRequested && Closed is { } closed) await closed();
    }

    private Task SetState(HubState state)
    {
        State = state;
        StateChanged?.Invoke();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        await _connection.DisposeAsync();
        _stop.Dispose();
    }
}
```

Notes on `GameConnection`:
- **Exceptions:** the calls catch every exception on purpose. A call into a dead connection throws `InvalidOperationException`, and the server's generic failure throws `HubException`. Both become "Something went wrong." (spec §11). The pages never offer an action while the hub is down, so this is a fallback, not the normal path.
- **The action's type name:** SignalR writes each argument as its runtime type, which would leave out the action's `"type"` name. So the action is written as a `PlayerAction` first, as the server tests' client does.
- **`AddJsonProtocol`** is an extension in `Microsoft.Extensions.DependencyInjection`, hence that using.

Create `src/CromoBound.Client/Services/LobbyState.cs`:

```csharp
using CromoBound.Contracts;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;

namespace CromoBound.Client.Services;

/// <summary>Everything the pages show about the lobby and the player's match, folded from <c>GetLobby</c>, <c>GetMatch</c> and the hub's
/// notices (spec §6). It also turns notices into the short notifications the layout pops up (spec §6.3).</summary>
public sealed class LobbyState(SessionState session) : IGameClient
{
    private List<PlayerPresence> _players = [];
    private List<ChallengeInfo> _received = [];
    private MatchEndedNotice? _abandoned;

    public bool Loaded { get; private set; }
    public IReadOnlyList<PlayerPresence> Players => _players;
    public int Online => _players.Count(p => p.Online);

    /// <summary>Challenges others sent the player, by challenger name.</summary>
    public IReadOnlyList<ChallengeInfo> Received => _received;

    /// <summary>The player's own open challenge; the server allows one.</summary>
    public ChallengeInfo? Sent { get; private set; }

    public Guid? MatchId { get; private set; }
    public string? Opponent { get; private set; }
    public PlayerId? Seat { get; private set; }

    /// <summary>The player's latest view of <see cref="MatchId"/>. (The name <c>View</c> is taken by the notice method.)</summary>
    public PlayerView? CurrentView { get; private set; }

    /// <summary>The last match of the player's that ended while the app was open.</summary>
    public MatchEndedNotice? Ended { get; private set; }

    public bool Maintenance { get; private set; }
    public HubState Connection { get; private set; } = HubState.Connecting;
    public bool IsConnected => Connection == HubState.Connected;

    public event Action? Changed;

    /// <summary>A player came, went or started or ended a match, or maintenance switched: what the maintenance page counts.</summary>
    public event Action? PlayersChanged;

    public event Action<string>? Notice;
    public event Action<Guid>? MatchBegun;

    private string Me => session.Me?.UserName ?? "";

    public void Load(LobbyReply lobby, MatchReply? match)
    {
        _players = Sorted(lobby.Players);
        _received = [.. lobby.Challenges.Where(c => !IsMine(c)).OrderBy(c => c.From, StringComparer.OrdinalIgnoreCase)];
        Sent = lobby.Challenges.FirstOrDefault(IsMine);
        if (lobby.MatchId != MatchId) (Opponent, Seat, CurrentView) = (null, null, null);
        MatchId = lobby.MatchId;
        if (match is { MatchId: { } id } && id == MatchId)
        {
            Opponent = match.Opponent;
            CurrentView = match.View;
            Seat = match.View?.Viewer ?? Seat;
        }
        if (lobby.Ended is { } ended) _abandoned = ended;
        Maintenance = lobby.Maintenance;
        Loaded = true;
        PlayersChanged?.Invoke();
        Changed?.Invoke();
    }

    public void SetConnection(HubState state)
    {
        Connection = state;
        Changed?.Invoke();
    }

    /// <summary>The server doesn't tell the challenger's own tabs about a new challenge, so the tab that sent it records it.</summary>
    public void ChallengeSent(ChallengeInfo challenge)
    {
        Sent = challenge;
        Changed?.Invoke();
    }

    /// <summary>A challenge the server says no longer exists.</summary>
    public void ChallengeGone(Guid challengeId)
    {
        if (Sent?.ChallengeId == challengeId) Sent = null;
        _received.RemoveAll(c => c.ChallengeId == challengeId);
        Changed?.Invoke();
    }

    /// <summary>The notice of a match abandoned at startup, once.</summary>
    public MatchEndedNotice? TakeAbandoned()
    {
        var abandoned = _abandoned;
        _abandoned = null;
        return abandoned;
    }

    public Task ChallengeReceived(ChallengeNotice challenge)
    {
        if (_received.Any(c => c.ChallengeId == challenge.ChallengeId)) return Task.CompletedTask;
        _received = [.. _received.Append(new ChallengeInfo(challenge.ChallengeId, challenge.From, Me, challenge.Format))
            .OrderBy(c => c.From, StringComparer.OrdinalIgnoreCase)];
        Notice?.Invoke($"{challenge.From} challenges you to {Formats.InSentence(challenge.Format)}.");
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task ChallengeClosed(ChallengeClosedNotice closed)
    {
        string? notice;
        if (Sent is { } sent && sent.ChallengeId == closed.ChallengeId)
        {
            Sent = null;
            notice = closed.Reason switch
            {
                ChallengeEnd.Declined => $"{sent.To} declined your challenge.",
                ChallengeEnd.Withdrawn => $"Your challenge to {sent.To} was withdrawn.",
                _ => null,
            };
        }
        else if (_received.Find(c => c.ChallengeId == closed.ChallengeId) is { } received)
        {
            _received.Remove(received);
            notice = closed.Reason switch
            {
                ChallengeEnd.Cancelled => $"{received.From} cancelled their challenge.",
                ChallengeEnd.Withdrawn => $"{received.From}'s challenge was withdrawn.",
                _ => null,
            };
        }
        else
        {
            return Task.CompletedTask;
        }
        if (notice is not null) Notice?.Invoke(notice);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task MatchStarted(MatchStartedNotice started)
    {
        (MatchId, Opponent, Seat, CurrentView, Ended) = (started.MatchId, started.Opponent, started.Seat, null, null);
        MatchBegun?.Invoke(started.MatchId);
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task View(MatchViewNotice view)
    {
        if (view.MatchId != MatchId) return Task.CompletedTask;
        CurrentView = view.View;
        Seat = view.View.Viewer;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task MatchEnded(MatchEndedNotice ended)
    {
        if (ended.MatchId == MatchId) MatchId = null;
        Ended = ended;
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task PlayerChanged(PlayerPresence player)
    {
        _players = Sorted(_players.Where(p => !SameName(p.UserName, player.UserName)).Append(player));
        PlayersChanged?.Invoke();
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task PlayerLeft(PlayerLeftNotice left)
    {
        _players.RemoveAll(p => SameName(p.UserName, left.UserName));
        PlayersChanged?.Invoke();
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    public Task MaintenanceChanged(MaintenanceNotice maintenance)
    {
        Maintenance = maintenance.On;
        PlayersChanged?.Invoke();
        Changed?.Invoke();
        return Task.CompletedTask;
    }

    private bool IsMine(ChallengeInfo challenge) => SameName(challenge.From, Me);

    private static bool SameName(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static List<PlayerPresence> Sorted(IEnumerable<PlayerPresence> players) =>
        [.. players.OrderBy(p => p.UserName, StringComparer.OrdinalIgnoreCase)];
}
```

`LobbyState`'s view property is `CurrentView`: a type can't have a property and a method with one name, and the notice method must be called `View`, the name the server pushes it under.

Create `src/CromoBound.Client/Services/LobbySync.cs`:

```csharp
namespace CromoBound.Client.Services;

/// <summary>Feeds <see cref="LobbyState"/> from the hub (spec §5.2): notices as they come, the whole lobby again after every connect and
/// reconnect, and, when the server closes the connection, a session check before connecting again.</summary>
public sealed class LobbySync(IGameHub hub, LobbyState state, IServerApi api, SessionState session)
{
    private bool _started;

    public Task StartAsync()
    {
        if (_started) return Task.CompletedTask;
        _started = true;
        hub.Listen(state);
        hub.StateChanged += () => state.SetConnection(hub.State);
        hub.Connected += ReloadAsync;
        hub.Closed += ClosedAsync;
        return hub.StartAsync();
    }

    /// <summary>A failed call leaves the state as it is; the next reconnect tries again.</summary>
    public async Task ReloadAsync()
    {
        if (await hub.GetLobbyAsync() is not { } lobby) return;
        var match = lobby.MatchId is null ? null : await hub.GetMatchAsync();
        state.Load(lobby, match);
    }

    private async Task ClosedAsync()
    {
        await api.MeAsync();
        if (!session.IsEnded) await hub.StartAsync();
    }
}
```

In `src/CromoBound.Client/Layout/MainLayout.razor`:

1. Add these injections under the existing ones:

```razor
@inject LobbyState Lobby
@inject LobbySync Sync
@inject ISnackbar Snackbar
@inject IDialogService Dialogs
```

2. Put the banners between the closing `</header>` and the `@if (failure is not null)` block:

```razor
@if (ready && Lobby.Connection == HubState.Reconnecting)
{
    <div class="cb-banner warn" role="status">
        <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12a9 9 0 1 1-3-6.7"></path><path d="M21 4v5h-5"></path></svg>
        <span><strong>Reconnecting...</strong> Your actions wait until the connection is back.</span>
    </div>
}
@if (ready && Lobby.Maintenance)
{
    <div class="cb-banner info" role="status">
        <svg aria-hidden="true" width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="9"></circle><path d="M12 8v5"></path><path d="M12 16h.01"></path></svg>
        <span><strong>The server is in maintenance.</strong> No new challenges for now. Running matches go on.</span>
    </div>
}
```

3. Replace `OnSignedInAsync` with:

```csharp
    /// <summary>Connects the hub without waiting for it: the pages show their state and disable what needs the hub until it's up.</summary>
    private Task OnSignedInAsync()
    {
        Lobby.Changed += OnLobbyChanged;
        Lobby.Notice += OnNotice;
        Lobby.MatchBegun += OnMatchBegun;
        _ = Sync.StartAsync();
        return Task.CompletedTask;
    }

    private void OnLobbyChanged() => _ = InvokeAsync(StateHasChanged);

    private void OnNotice(string text) => _ = InvokeAsync(() => Snackbar.Add(text, Severity.Info));

    private void OnMatchBegun(Guid matchId) => _ = InvokeAsync(() => Nav.NavigateTo($"match/{matchId}"));
```

4. Replace `Dispose` with:

```csharp
    public void Dispose()
    {
        Session.SessionEnded -= GoToLogin;
        Lobby.Changed -= OnLobbyChanged;
        Lobby.Notice -= OnNotice;
        Lobby.MatchBegun -= OnMatchBegun;
    }
```

`IDialogService` is injected now for the abandoned dialog, which Task 5 adds.

In `src/CromoBound.Client/Program.cs`, add after the `SessionKeeper` registration:

```csharp
builder.Services.AddScoped<IGameHub, GameConnection>();
builder.Services.AddScoped<LobbyState>();
builder.Services.AddScoped<LobbySync>();
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Client tests/CromoBound.Client.Tests
git commit -m "feat(client): connect to the hub and keep the lobby state"
```

---

### Task 4: Decks kept in the browser

**Files:**
- Create:
  - `src/CromoBound.Client/Services/IBrowserStorage.cs`, `Services/LocalBrowserStorage.cs`, `Services/DeckStore.cs`
  - `src/CromoBound.Client/Pages/Decks.razor`
  - `tests/CromoBound.Client.Tests/Fakes/MemoryStorage.cs`, `SampleDecks.cs`
  - `tests/CromoBound.Client.Tests/DeckStoreTests.cs`, `DecksPageTests.cs`
- Modify: `src/CromoBound.Client/Program.cs`, `tests/CromoBound.Client.Tests/Ui.cs`

**Interfaces:**
- Consumes: Task 2's `SessionState` and `Ui`; `Deck` and `DeckEntry` from `CromoBound.Models.Cards`; `CromoJson`.
- Produces:
  - `IBrowserStorage`: `GetAsync(string)`, which returns `string?`, and `SetAsync(string, string)`.
  - `StoredDeck(string Id, Deck Deck)`.
  - `DeckStore`:
    - `ListAsync()`, `AddAsync(string json)`, which returns `ApiResult<StoredDeck>`;
    - `RenameAsync(string id, string name)` and `DeleteAsync(string id)`, which return `ApiResult`;
    - the static `Counts(Deck)` and `KeyFor(string userName)`;
    - the constants `MaxBytes`, `Empty`, `NotJson`, `NotADeck`, `NoName`, `TooBig`, `NameNeeded`, `NoRoom`.
  - The page `/decks`.
  - In the tests: `MemoryStorage`, `SampleDecks.Json(...)`, `Ui.Storage`, `Ui.Decks`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Client.Tests/Fakes/MemoryStorage.cs`:

```csharp
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
```

Create `tests/CromoBound.Client.Tests/SampleDecks.cs`:

```csharp
namespace CromoBound.Client.Tests;

/// <summary>Deck JSON as a player pastes it. The client doesn't check legality, so made-up ids are fine.</summary>
internal static class SampleDecks
{
    public static string Json(string name = "Jinx aggro", int main = 40, int sideboard = 0)
    {
        var side = sideboard > 0 ? $$""", "sideboard": [ { "printing": "unit-2", "count": {{sideboard}} } ]""" : "";
        return $$"""
            {
              "name": "{{name}}",
              "legend": "legend-1",
              "champion": "champion-1",
              "main": [ { "printing": "unit-1", "count": {{main}} } ],
              "runes": [ { "printing": "rune-1", "count": 12 } ],
              "battlefields": [ "bf-1", "bf-2", "bf-3" ]{{side}}
            }
            """;
    }
}
```

In `tests/CromoBound.Client.Tests/Ui.cs`, add the properties:

```csharp
    public MemoryStorage Storage { get; } = new();
    public DeckStore Decks { get; }
```

and in the constructor, after the `LobbySync` registration:

```csharp
        Decks = new DeckStore(Storage, Session);
        Ctx.Services.AddSingleton<IBrowserStorage>(Storage);
        Ctx.Services.AddSingleton(Decks);
```

Create `tests/CromoBound.Client.Tests/DeckStoreTests.cs`:

```csharp
using CromoBound.Client.Services;
using CromoBound.Client.Tests.Fakes;
using CromoBound.Contracts;

namespace CromoBound.Client.Tests;

public class DeckStoreTests
{
    private static (DeckStore Store, MemoryStorage Storage, SessionState Session) Store(string user = "marco")
    {
        var session = new SessionState();
        session.SignedIn(new MeResponse(user, false));
        var storage = new MemoryStorage();
        return (new DeckStore(storage, session), storage, session);
    }

    [Fact]
    public async Task An_added_deck_is_listed_with_its_name_and_counts()
    {
        var (store, _, _) = Store();

        var added = await store.AddAsync(SampleDecks.Json("Jinx aggro", sideboard: 6));
        var decks = await store.ListAsync();

        Assert.True(added.Ok);
        var deck = Assert.Single(decks);
        Assert.Equal(("Jinx aggro", "legend-1"), (deck.Deck.Name, deck.Deck.Legend));
        Assert.Equal("40 main · 12 runes · 3 battlefields · sideboard 6", DeckStore.Counts(deck.Deck));
        Assert.Equal("40 main · 12 runes · 3 battlefields", DeckStore.Counts((await store.AddAsync(SampleDecks.Json("Vi"))).Value!.Deck));
    }

    [Theory]
    [InlineData("", DeckStore.Empty)]
    [InlineData("   ", DeckStore.Empty)]
    [InlineData("{ \"name\": \"Jinx", DeckStore.NotJson)]
    [InlineData("not json at all", DeckStore.NotJson)]
    [InlineData("{ \"name\": \"Jinx\" }", DeckStore.NotADeck)]
    [InlineData("[1, 2, 3]", DeckStore.NotADeck)]
    [InlineData("{ \"name\": \"Jinx\", \"legend\": \"l\", \"champion\": \"c\", \"colour\": \"red\" }", DeckStore.NotADeck)]
    [InlineData("{ \"name\": \" \", \"legend\": \"l\", \"champion\": \"c\" }", DeckStore.NoName)]
    public async Task Text_that_isnt_a_deck_is_refused_and_nothing_is_saved(string json, string error)
    {
        var (store, storage, _) = Store();

        var added = await store.AddAsync(json);

        Assert.Equal(error, added.Error);
        Assert.Empty(storage.Items);
    }

    [Fact]
    public async Task Text_over_a_megabyte_is_refused()
    {
        var (store, storage, _) = Store();

        var added = await store.AddAsync(SampleDecks.Json(new string('x', DeckStore.MaxBytes)));

        Assert.Equal(DeckStore.TooBig, added.Error);
        Assert.Empty(storage.Items);
    }

    [Fact]
    public async Task Decks_are_kept_per_user()
    {
        var (marco, storage, _) = Store("marco");
        await marco.AddAsync(SampleDecks.Json("Marco's deck"));
        var giuliaSession = new SessionState();
        giuliaSession.SignedIn(new MeResponse("giulia", false));
        var giulia = new DeckStore(storage, giuliaSession);
        var marcoAgain = new SessionState();
        marcoAgain.SignedIn(new MeResponse("Marco", false));

        Assert.Empty(await giulia.ListAsync());
        Assert.Equal("Marco's deck", Assert.Single(await new DeckStore(storage, marcoAgain).ListAsync()).Deck.Name);
        Assert.Equal(new[] { DeckStore.KeyFor("marco") }, storage.Items.Keys);
    }

    [Fact]
    public async Task A_deck_can_be_renamed_and_deleted()
    {
        var (store, _, _) = Store();
        var first = (await store.AddAsync(SampleDecks.Json("First"))).Value!;
        var second = (await store.AddAsync(SampleDecks.Json("Second"))).Value!;

        Assert.Equal(DeckStore.NameNeeded, (await store.RenameAsync(first.Id, "  ")).Error);
        Assert.True((await store.RenameAsync(first.Id, " Jinx aggro ")).Ok);
        Assert.True((await store.DeleteAsync(second.Id)).Ok);

        Assert.Equal("Jinx aggro", Assert.Single(await store.ListAsync()).Deck.Name);
    }

    [Fact]
    public async Task Unreadable_storage_reads_as_no_decks()
    {
        var (store, storage, _) = Store();
        storage.Items[DeckStore.KeyFor("marco")] = "garbage";

        Assert.Empty(await store.ListAsync());
    }

    [Fact]
    public async Task A_full_browser_storage_is_a_plain_error()
    {
        var (store, storage, _) = Store();
        storage.Full = true;

        Assert.Equal(DeckStore.NoRoom, (await store.AddAsync(SampleDecks.Json())).Error);
    }
}
```

Create `tests/CromoBound.Client.Tests/DecksPageTests.cs`:

```csharp
using Bunit;
using CromoBound.Client.Pages;
using CromoBound.Client.Services;
using Microsoft.AspNetCore.Components.Forms;

namespace CromoBound.Client.Tests;

public class DecksPageTests
{
    [Fact]
    public async Task Pasting_a_deck_adds_it_to_the_list()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<Decks>();

        cut.Find("#paste").Input(SampleDecks.Json("Jinx aggro"));
        await cut.ClickAsync("#add-deck");

        cut.WaitForAssertion(() => Assert.Equal("Jinx aggro", cut.Find(".cb-deck-name").TextContent));
        Assert.Equal("40 main · 12 runes · 3 battlefields", cut.Find(".cb-deck-counts").TextContent);
        Assert.Equal("", cut.Find("#paste").GetAttribute("value") ?? "");
    }

    [Fact]
    public async Task Pasting_garbage_shows_why_and_keeps_the_text()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<Decks>();

        cut.Find("#paste").Input("{ broken");
        await cut.ClickAsync("#add-deck");

        cut.WaitForAssertion(() => Assert.Equal(DeckStore.NotJson, cut.Find("[role=alert]").TextContent));
        Assert.Empty(cut.FindAll(".cb-deckbox"));
        Assert.Empty(ui.Storage.Items);
    }

    [Fact]
    public async Task An_uploaded_file_fills_the_box()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<Decks>();

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromText(SampleDecks.Json("From a file"), "deck.json"));

        cut.WaitForAssertion(() => Assert.Contains("From a file", cut.Find("#paste").GetAttribute("value")));
    }

    [Fact]
    public async Task A_file_that_is_too_big_is_refused()
    {
        await using var ui = new Ui();
        var cut = ui.Ctx.Render<Decks>();

        cut.FindComponent<InputFile>().UploadFiles(InputFileContent.CreateFromBinary(new byte[DeckStore.MaxBytes + 1], "big.json"));

        cut.WaitForAssertion(() => Assert.Equal(DeckStore.TooBig, cut.Find("[role=alert]").TextContent));
        Assert.Equal("", cut.Find("#paste").GetAttribute("value") ?? "");
    }

    [Fact]
    public async Task A_deck_is_renamed_in_place_and_deleted()
    {
        await using var ui = new Ui();
        await ui.Decks.AddAsync(SampleDecks.Json("Old name"));
        var cut = ui.Ctx.Render<Decks>();
        cut.WaitForAssertion(() => Assert.Equal("Old name", cut.Find(".cb-deck-name").TextContent));

        await cut.ClickAsync(".rename");
        cut.Find(".rename-input").Input("New name");
        await cut.ClickAsync(".rename-save");
        cut.WaitForAssertion(() => Assert.Equal("New name", cut.Find(".cb-deck-name").TextContent));

        await cut.ClickAsync(".delete");
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".cb-deckbox")));
        Assert.Empty(await ui.Decks.ListAsync());
    }
}
```

The sample-deck helper is `SampleDecks`, not `Decks`: inside the test namespace, a helper named `Decks` would hide the page `CromoBound.Client.Pages.Decks`.

If bUnit 2.11 names the file-upload helper differently from `UploadFiles` and `InputFileContent.CreateFromText` / `CreateFromBinary`, use bUnit 2's equivalent and name it in the report.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`DeckStore`, `IBrowserStorage` and the page `Decks` don't exist).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Client/Services/IBrowserStorage.cs`:

```csharp
namespace CromoBound.Client.Services;

/// <summary>The browser's local storage: strings by key, in this browser only.</summary>
public interface IBrowserStorage
{
    ValueTask<string?> GetAsync(string key);

    /// <summary>Throws when the browser refuses the write (its storage is full).</summary>
    ValueTask SetAsync(string key, string value);
}
```

Create `src/CromoBound.Client/Services/LocalBrowserStorage.cs`:

```csharp
using Microsoft.JSInterop;

namespace CromoBound.Client.Services;

public sealed class LocalBrowserStorage(IJSRuntime js) : IBrowserStorage
{
    public ValueTask<string?> GetAsync(string key) => js.InvokeAsync<string?>("localStorage.getItem", key);

    public ValueTask SetAsync(string key, string value) => js.InvokeVoidAsync("localStorage.setItem", key, value);
}
```

Create `src/CromoBound.Client/Services/DeckStore.cs`:

```csharp
using System.Text;
using System.Text.Json;
using CromoBound.Models.Cards;
using CromoBound.Models.Json;

namespace CromoBound.Client.Services;

/// <summary>A deck the player keeps in this browser, with the id the app gave it.</summary>
public sealed record StoredDeck(string Id, Deck Deck);

/// <summary>The player's decks in the browser's local storage, under a key with their username, so two accounts on one computer
/// don't mix (spec §7). A deck only has to read as a <see cref="Deck"/>; legality is the server's, at challenge and accept time.</summary>
public sealed class DeckStore(IBrowserStorage storage, SessionState session)
{
    public const int MaxBytes = 1_000_000;

    public const string Empty = "Paste a deck or upload a file first.";
    public const string NotJson = "That isn't a deck the app can read: it isn't valid JSON.";
    public const string NotADeck = "That isn't a deck the app can read: it doesn't have a deck's fields.";
    public const string NoName = "That isn't a deck the app can read: the deck has no name.";
    public const string TooBig = "That file is too big to be a deck.";
    public const string NameNeeded = "A deck needs a name.";
    public const string NoRoom = "The browser has no room for more decks.";
    public const string Missing = "That deck isn't here any more.";

    /// <summary>Usernames are unique in any letter case, so the key uses the lower-case name.</summary>
    public static string KeyFor(string userName) => "cromobound.decks." + userName.ToLowerInvariant();

    public static string Counts(Deck deck)
    {
        var counts = $"{deck.Main.Sum(e => e.Count)} main · {deck.Runes.Sum(e => e.Count)} runes · {deck.Battlefields.Count} battlefields";
        var sideboard = deck.Sideboard.Sum(e => e.Count);
        return sideboard > 0 ? $"{counts} · sideboard {sideboard}" : counts;
    }

    /// <summary>Unreadable storage (edited by hand, or from an older app) reads as no decks.</summary>
    public async Task<IReadOnlyList<StoredDeck>> ListAsync()
    {
        var json = await storage.GetAsync(Key);
        if (string.IsNullOrEmpty(json)) return [];
        try
        {
            return CromoJson.Deserialize<List<StoredDeck>>(json);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public async Task<ApiResult<StoredDeck>> AddAsync(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Fail(Empty);
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes) return Fail(TooBig);
        try
        {
            JsonDocument.Parse(json).Dispose();
        }
        catch (JsonException)
        {
            return Fail(NotJson);
        }
        Deck deck;
        try
        {
            deck = CromoJson.Deserialize<Deck>(json);
        }
        catch (JsonException)
        {
            return Fail(NotADeck);
        }
        if (string.IsNullOrWhiteSpace(deck.Name)) return Fail(NoName);
        var stored = new StoredDeck(Guid.NewGuid().ToString("N"), deck with { Name = deck.Name.Trim() });
        var error = await SaveAsync([.. await ListAsync(), stored]);
        return error is null ? new ApiResult<StoredDeck>(stored, null) : Fail(error);
    }

    public async Task<ApiResult> RenameAsync(string id, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return new ApiResult(NameNeeded);
        var decks = (await ListAsync()).ToList();
        var index = decks.FindIndex(d => d.Id == id);
        if (index < 0) return new ApiResult(Missing);
        decks[index] = decks[index] with { Deck = decks[index].Deck with { Name = name.Trim() } };
        return new ApiResult(await SaveAsync(decks));
    }

    public async Task<ApiResult> DeleteAsync(string id) =>
        new(await SaveAsync([.. (await ListAsync()).Where(d => d.Id != id)]));

    private string Key => KeyFor(session.Me?.UserName ?? "");

    /// <summary>The browser refuses a write when its storage is full; any failure there reads the same to the player.</summary>
    private async Task<string?> SaveAsync(List<StoredDeck> decks)
    {
        try
        {
            await storage.SetAsync(Key, CromoJson.Serialize(decks));
            return null;
        }
        catch (Exception)
        {
            return NoRoom;
        }
    }

    private static ApiResult<StoredDeck> Fail(string error) => new(null, error);
}
```

`CromoJson` disallows unknown fields, so a pasted file with a field the `Deck` format doesn't have reads as `NotADeck`. The "colour" test pins that. It also leaves out empty lists when writing, and `Deck`'s lists default to empty, so a stored deck reads back the same.

Create `src/CromoBound.Client/Pages/Decks.razor`:

```razor
@page "/decks"
@inject DeckStore Store

<main class="cb-main">
    <div>
        <h1 class="cb-title">Your decks</h1>
        <p class="cb-sub">Kept in this browser only. The server checks a deck when you challenge or accept.</p>
    </div>
    <div class="cb-row">
        <section class="cb-col-main" aria-label="Saved decks">
            @if (decks.Count == 0)
            {
                <p class="cb-sub">You have no decks yet. Paste one on the right.</p>
            }
            <div class="cb-deck-grid">
                @foreach (var deck in decks)
                {
                    <div>
                        <div class="cb-deckbox">
                            <span class="back" aria-hidden="true"></span>
                            <span class="mid" aria-hidden="true"></span>
                            <div class="front">
                                <div class="label">
                                    @if (renaming == deck.Id)
                                    {
                                        <label class="cb-label" for="rename-@deck.Id">New name</label>
                                        <input id="rename-@deck.Id" class="cb-input rename-input" @bind="newName" @bind:event="oninput" />
                                    }
                                    else
                                    {
                                        <span class="cb-deck-name">@deck.Deck.Name</span>
                                        <span class="cb-deck-counts">@DeckStore.Counts(deck.Deck)</span>
                                    }
                                </div>
                            </div>
                        </div>
                        <div class="cb-actions">
                            @if (renaming == deck.Id)
                            {
                                <MudButton Class="rename-cancel" Variant="Variant.Text" Color="Color.Secondary" OnClick="() => renaming = null">Cancel</MudButton>
                                <MudButton Class="rename-save" Variant="Variant.Filled" Color="Color.Primary" OnClick="() => SaveNameAsync(deck)">Save</MudButton>
                            }
                            else
                            {
                                <MudButton Class="rename" Variant="Variant.Text" Color="Color.Secondary" OnClick="() => StartRename(deck)" aria-label="@($"Rename {deck.Deck.Name}")">Rename</MudButton>
                                <MudButton Class="delete cb-danger-text" Variant="Variant.Text" OnClick="() => DeleteAsync(deck)" aria-label="@($"Delete {deck.Deck.Name}")">Delete</MudButton>
                            }
                        </div>
                    </div>
                }
            </div>
        </section>
        <section class="cb-col-side cb-panel" aria-labelledby="add-title">
            <h2 id="add-title" class="cb-section-title">Add a deck</h2>
            <div class="cb-field">
                <label class="cb-label" for="paste">Paste the deck's JSON</label>
                <textarea id="paste" class="cb-input @(error is null ? "" : "invalid")" rows="9" @bind="text" @bind:event="oninput"></textarea>
            </div>
            @if (error is not null)
            {
                <p class="cb-error" role="alert">@error</p>
            }
            <div class="cb-actions">
                <label class="cb-upload">
                    Upload a .json file
                    <InputFile accept=".json,application/json" OnChange="UploadAsync" class="cb-visually-hidden" />
                </label>
                <MudButton id="add-deck" Variant="Variant.Filled" Color="Color.Primary" OnClick="AddAsync">Add deck</MudButton>
            </div>
        </section>
    </div>
</main>

@code {
    private IReadOnlyList<StoredDeck> decks = [];
    private string text = "";
    private string? error;
    private string? renaming;
    private string newName = "";

    protected override async Task OnInitializedAsync() => decks = await Store.ListAsync();

    private async Task AddAsync()
    {
        var added = await Store.AddAsync(text);
        error = added.Error;
        if (!added.Ok) return;
        text = "";
        decks = await Store.ListAsync();
    }

    /// <summary>Fills the box with the file, so the player sees it before adding; a file over the limit isn't read at all.</summary>
    private async Task UploadAsync(InputFileChangeEventArgs e)
    {
        if (e.File.Size > DeckStore.MaxBytes)
        {
            error = DeckStore.TooBig;
            return;
        }
        using var reader = new StreamReader(e.File.OpenReadStream(DeckStore.MaxBytes));
        text = await reader.ReadToEndAsync();
        error = null;
    }

    private void StartRename(StoredDeck deck)
    {
        renaming = deck.Id;
        newName = deck.Deck.Name;
    }

    private async Task SaveNameAsync(StoredDeck deck)
    {
        var renamed = await Store.RenameAsync(deck.Id, newName);
        error = renamed.Error;
        if (!renamed.Ok) return;
        renaming = null;
        decks = await Store.ListAsync();
    }

    private async Task DeleteAsync(StoredDeck deck)
    {
        error = (await Store.DeleteAsync(deck.Id)).Error;
        decks = await Store.ListAsync();
    }
}
```

Add to `src/CromoBound.Client/wwwroot/css/app.css`:

```css
.cb-upload { display: inline-flex; align-items: center; min-height: 44px; padding: 0 18px; box-sizing: border-box; border-radius: 999px; cursor: pointer; font-weight: 600; font-size: 14px; color: var(--cb-accent); border: 1px solid var(--cb-accent-edge); }
.cb-upload:focus-within { outline: 2px solid var(--cb-accent); outline-offset: 2px; }
.cb-visually-hidden { position: absolute; width: 1px; height: 1px; overflow: hidden; clip: rect(0 0 0 0); white-space: nowrap; }
```

In `src/CromoBound.Client/Program.cs`, add after the `LobbySync` registration:

```csharp
builder.Services.AddScoped<IBrowserStorage, LocalBrowserStorage>();
builder.Services.AddScoped<DeckStore>();
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Client tests/CromoBound.Client.Tests
git commit -m "feat(client): keep pasted decks in the browser"
```

---
### Task 5: The lobby page and its dialogs

**Files:**
- Create:
  - `src/CromoBound.Client/Dialogs/DialogResults.cs`, `Dialogs/ChallengeDialog.razor`, `Dialogs/AcceptDialog.razor`, `Dialogs/DeckProblemsDialog.razor`, `Dialogs/AbandonedDialog.razor`
  - `tests/CromoBound.Client.Tests/LobbyPageTests.cs`
- Modify:
  - `src/CromoBound.Client/Pages/Lobby.razor` (replace the placeholder)
  - `src/CromoBound.Client/_Imports.razor`, `Layout/MainLayout.razor`, `wwwroot/css/app.css`
  - `tests/CromoBound.Client.Tests/Ui.cs`, `LayoutTests.cs`

**Interfaces:**
- Consumes:
  - Task 3's `LobbyState`, `LobbySync`, `IGameHub`, `FakeGameHub`, `Formats` and `GameConnection.Unexpected`.
  - Task 4's `DeckStore`, `StoredDeck`, `SampleDecks`.
  - `DeckIssue` from `CromoBound.Data`.
- Produces:
  - `ChallengeChoice(MatchFormat Format, StoredDeck Deck)`.
  - `AcceptAnswer` {`Accept`, `Decline`} and `AcceptChoice(AcceptAnswer Answer, StoredDeck? Deck)`.
  - `DeckProblemsAnswer` {`GoToDecks`, `PickAnother`}.
  - `DialogDefaults.Options`.
  - The dialogs:
    - `ChallengeDialog` (`Decks`, `Format`, `DeckId`);
    - `AcceptDialog` (`Challenge`, `Decks`, `DeckId`);
    - `DeckProblemsDialog` (`DeckName`, `Problems`);
    - `AbandonedDialog`.
  - The lobby page `/`.
  - In the tests: `Ui.RenderDialogs()`, `Ui.RenderInLayout<T>()`.

- [ ] **Step 1: Write the failing tests**

In `tests/CromoBound.Client.Tests/Ui.cs`, add these members (with `using Microsoft.AspNetCore.Components;` and `using MudBlazor;` at the top if they aren't there yet):

```csharp
    /// <summary>Dialogs show in the provider, which a page rendered on its own doesn't have.</summary>
    public IRenderedComponent<MudDialogProvider> RenderDialogs() => Ctx.Render<MudDialogProvider>();

    /// <summary>A page inside the real layout, which brings its own providers, banners and global navigation.</summary>
    public IRenderedComponent<MainLayout> RenderInLayout<TPage>() where TPage : IComponent =>
        Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, b =>
        {
            b.OpenComponent<TPage>(0);
            b.CloseComponent();
        }));
```

Create `tests/CromoBound.Client.Tests/LobbyPageTests.cs`:

```csharp
using Bunit;
using CromoBound.Client.Pages;
using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Data;
using CromoBound.Engine.Matches;

namespace CromoBound.Client.Tests;

public class LobbyPageTests
{
    private static readonly Guid FromGiulia = Guid.Parse("00000000-0000-0000-0000-00000000000a");

    private static async Task<Ui> LobbyAsync(
        IReadOnlyList<PlayerPresence>? players = null, IReadOnlyList<ChallengeInfo>? challenges = null, Guid? match = null,
        bool maintenance = false, HubState connection = HubState.Connected, int decks = 2)
    {
        var ui = new Ui();
        if (decks > 0) await ui.Decks.AddAsync(SampleDecks.Json("Jinx aggro"));
        if (decks > 1) await ui.Decks.AddAsync(SampleDecks.Json("Vi midrange"));
        ui.Lobby.Load(new LobbyReply(
            players ?? [new("giulia", true, false), new("sara", true, true), new("tommaso", false, false)],
            challenges ?? [], match, null, maintenance), null);
        ui.Lobby.SetConnection(connection);
        return ui;
    }

    private static bool CanChallenge(IRenderedComponent<Lobby> cut, string player) =>
        !cut.Find($"[data-player='{player}'] .challenge").HasAttribute("disabled");

    [Fact]
    public async Task Players_show_their_status_and_who_can_be_challenged()
    {
        await using var ui = await LobbyAsync();
        var cut = ui.Ctx.Render<Lobby>();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".cb-player").Count));
        Assert.Equal("Pick an opponent and a deck. 2 of your friends are online.", cut.Find(".cb-sub").TextContent);
        Assert.Equal(new[] { "Online", "In a match", "Offline" }, cut.FindAll(".cb-status").Select(s => s.TextContent.Trim()));
        Assert.True(CanChallenge(cut, "giulia"));
        Assert.False(CanChallenge(cut, "sara"));
        Assert.True(CanChallenge(cut, "tommaso"));
        Assert.Contains("Your decks (2)", cut.Markup);
        Assert.Equal("You haven't challenged anyone.", cut.Find(".cb-sent .cb-sub").TextContent);
    }

    [Fact]
    public async Task Challenging_sends_the_chosen_format_and_deck()
    {
        await using var ui = await LobbyAsync();
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Lobby>();
        var vi = (await ui.Decks.ListAsync())[1];

        await cut.ClickAsync("[data-player='giulia'] .challenge");
        dialogs.WaitForElement("#send-challenge");
        await dialogs.InvokeAsync(() => dialogs.Find("#bo3").Change(true));
        await dialogs.InvokeAsync(() => dialogs.Find("#deck").Change(vi.Id));
        await dialogs.ClickAsync("#send-challenge");

        cut.WaitForAssertion(() => Assert.Equal(new[] { "Challenge giulia Bo3 Vi midrange" }, ui.Hub.Calls));
        cut.WaitForAssertion(() => Assert.Equal("You challenged giulia to a best of three. Waiting for an answer.", cut.Find(".cb-sent div").TextContent));
        Assert.False(CanChallenge(cut, "tommaso"));
        Assert.Contains("You have an open challenge. Cancel it to challenge someone else.", cut.Markup);
    }

    [Fact]
    public async Task A_refused_deck_shows_the_servers_problems_and_lets_you_pick_another()
    {
        await using var ui = await LobbyAsync();
        ui.Hub.Replies["Challenge"] = new HubReply(null, "That deck isn't legal.",
            [new DeckIssue(DeckIssueCode.MainDeckSize, DeckIssueSeverity.Incomplete, [], "The main deck has 38 cards; it needs 40.")]);
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Lobby>();

        await cut.ClickAsync("[data-player='giulia'] .challenge");
        await dialogs.ClickAsync("#send-challenge");
        dialogs.WaitForAssertion(() => Assert.Equal("The main deck has 38 cards; it needs 40.", dialogs.Find(".cb-problems li").TextContent));
        Assert.Contains("Jinx aggro", dialogs.Find(".cb-problems").TextContent);
        await dialogs.ClickAsync("#pick-another");
        await dialogs.ClickAsync("#send-challenge");

        cut.WaitForAssertion(() => Assert.Equal(2, ui.Hub.Calls.Count(c => c.StartsWith("Challenge giulia", StringComparison.Ordinal))));
        cut.WaitForAssertion(() => Assert.NotNull(ui.Lobby.Sent));
    }

    [Fact]
    public async Task Without_decks_the_challenge_dialog_points_to_the_decks_page()
    {
        await using var ui = await LobbyAsync(decks: 0);
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Lobby>();

        await cut.ClickAsync("[data-player='giulia'] .challenge");

        dialogs.WaitForAssertion(() => Assert.Contains("You have no decks yet.", dialogs.Markup));
        Assert.True(dialogs.Find("#send-challenge").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Accepting_sends_your_deck_and_declining_from_the_dialog_declines()
    {
        await using var ui = await LobbyAsync(challenges: [new(FromGiulia, "giulia", "marco", MatchFormat.Bo3)]);
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Lobby>();
        cut.WaitForAssertion(() => Assert.Contains("challenges you", cut.Find(".cb-received").TextContent));
        Assert.Equal("Best of three", cut.Find(".cb-received .cb-chip").TextContent);

        await cut.ClickAsync(".cb-received .accept");
        dialogs.WaitForAssertion(() => Assert.Contains("giulia challenges you to a best of three. Pick the deck you play with.", dialogs.Markup));
        await dialogs.ClickAsync("#accept-and-play");
        cut.WaitForAssertion(() => Assert.Contains($"Accept {FromGiulia} Jinx aggro", ui.Hub.Calls));

        await cut.ClickAsync(".cb-received .accept");
        await dialogs.ClickAsync("#decline-in-dialog");
        cut.WaitForAssertion(() => Assert.Contains($"Decline {FromGiulia}", ui.Hub.Calls));
    }

    [Fact]
    public async Task Cancelling_your_challenge_and_declining_from_the_ticket_call_the_hub()
    {
        var sent = Guid.NewGuid();
        await using var ui = await LobbyAsync(challenges: [new(FromGiulia, "giulia", "marco", MatchFormat.Bo1), new(sent, "marco", "luca", MatchFormat.Bo1)]);
        var cut = ui.Ctx.Render<Lobby>();

        await cut.ClickAsync("#cancel-challenge");
        await cut.ClickAsync(".cb-received .decline");

        cut.WaitForAssertion(() => Assert.Equal(new[] { $"Cancel {sent}", $"Decline {FromGiulia}" }, ui.Hub.Calls));
    }

    [Fact]
    public async Task A_refused_call_shows_the_message_and_reloads_the_lobby()
    {
        await using var ui = await LobbyAsync(challenges: [new(FromGiulia, "giulia", "marco", MatchFormat.Bo1)]);
        ui.Hub.Replies["Decline"] = HubReply.Fail("There is no such challenge.");
        ui.Hub.Lobby = new LobbyReply([new("giulia", true, false)], [], null, null, false);
        var cut = ui.Ctx.Render<Lobby>();

        await cut.ClickAsync(".cb-received .decline");

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".cb-received")));
        Assert.Contains("GetLobby", ui.Hub.Calls);
        Assert.Contains("There is no such challenge.", ui.Notices);
    }

    [Fact]
    public async Task While_reconnecting_the_banner_shows_and_challenging_is_off()
    {
        await using var ui = await LobbyAsync(
            challenges: [new(FromGiulia, "giulia", "marco", MatchFormat.Bo1)], connection: HubState.Reconnecting);

        var cut = ui.RenderInLayout<Lobby>();

        cut.WaitForAssertion(() => Assert.Contains("Reconnecting...", cut.Find(".cb-banner.warn").TextContent));
        Assert.All(cut.FindAll(".challenge"), b => Assert.True(b.HasAttribute("disabled")));
        Assert.True(cut.Find(".cb-received .accept").HasAttribute("disabled"));
        Assert.True(cut.Find(".cb-received .decline").HasAttribute("disabled"));
    }

    [Fact]
    public async Task In_maintenance_challenging_and_accepting_are_off()
    {
        await using var ui = await LobbyAsync(challenges: [new(FromGiulia, "giulia", "marco", MatchFormat.Bo1)], maintenance: true);
        var cut = ui.Ctx.Render<Lobby>();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll(".cb-player").Count));
        Assert.All(cut.FindAll(".challenge"), b => Assert.True(b.HasAttribute("disabled")));
        Assert.True(cut.Find(".cb-received .accept").HasAttribute("disabled"));
        Assert.False(cut.Find(".cb-received .decline").HasAttribute("disabled"));
    }

    [Fact]
    public async Task A_running_match_takes_you_to_its_page()
    {
        var match = Guid.NewGuid();
        await using var ui = await LobbyAsync(match: match);

        ui.Ctx.Render<Lobby>();

        Assert.EndsWith($"/match/{match}", ui.Nav.Uri);
    }
}
```

Add to `tests/CromoBound.Client.Tests/LayoutTests.cs`:

```csharp
    [Fact]
    public async Task A_match_abandoned_at_startup_is_explained_once()
    {
        await using var ui = new Ui();
        ui.Hub.Lobby = new LobbyReply([], [], null, new MatchEndedNotice(Guid.NewGuid(), MatchEndReason.Abandoned, [0, 0], null), false);
        var cut = ui.Ctx.Render<MainLayout>(ps => ps.Add(p => p.Body, "<p id='page'>page</p>"));
        cut.WaitForAssertion(() => Assert.Equal(1, ui.Hub.Starts));

        await cut.InvokeAsync(() => ui.Hub.SetStateAsync(HubState.Connected));

        cut.WaitForAssertion(() => Assert.Contains("The server was updated while your match was running", cut.Markup));
        await cut.ClickAsync("#abandoned-ok");
        await cut.InvokeAsync(() => ui.Lobby.SetConnection(HubState.Connected));
        cut.WaitForAssertion(() => Assert.DoesNotContain("The server was updated while your match was running", cut.Markup));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (the dialogs don't exist, and the placeholder lobby has no state to show).

- [ ] **Step 3: Write the implementation**

Add to `src/CromoBound.Client/_Imports.razor`:

```razor
@using CromoBound.Client.Dialogs
@using CromoBound.Engine.Matches
```

Create `src/CromoBound.Client/Dialogs/DialogResults.cs`:

```csharp
using CromoBound.Client.Services;
using CromoBound.Engine.Matches;
using MudBlazor;

namespace CromoBound.Client.Dialogs;

public sealed record ChallengeChoice(MatchFormat Format, StoredDeck Deck);

public enum AcceptAnswer { Accept, Decline }

/// <summary>"Not now" is the dialog's cancel; Decline and Accept are answers.</summary>
public sealed record AcceptChoice(AcceptAnswer Answer, StoredDeck? Deck);

public enum DeckProblemsAnswer { GoToDecks, PickAnother }

public static class DialogDefaults
{
    public static DialogOptions Options { get; } = new() { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
}
```

Create `src/CromoBound.Client/Dialogs/ChallengeDialog.razor`:

```razor
<MudDialog>
    <DialogContent>
        <div class="cb-dialog-body">
            <fieldset class="cb-fieldset">
                <legend class="cb-label">Format</legend>
                <div class="cb-choices">
                    <label class="cb-choice @(format == MatchFormat.Bo1 ? "on" : "")">
                        <input id="bo1" type="radio" name="format" checked="@(format == MatchFormat.Bo1)" @onchange="() => format = MatchFormat.Bo1" />
                        Best of one
                    </label>
                    <label class="cb-choice @(format == MatchFormat.Bo3 ? "on" : "")">
                        <input id="bo3" type="radio" name="format" checked="@(format == MatchFormat.Bo3)" @onchange="() => format = MatchFormat.Bo3" />
                        Best of three
                    </label>
                </div>
            </fieldset>
            @if (Decks.Count == 0)
            {
                <p class="cb-sub">You have no decks yet. <a href="decks" @onclick="Cancel">Add one on the decks page.</a></p>
            }
            else
            {
                <div class="cb-field">
                    <label class="cb-label" for="deck">Your deck</label>
                    <select id="deck" class="cb-input" @bind="deckId">
                        @foreach (var deck in Decks)
                        {
                            <option value="@deck.Id">@deck.Deck.Name</option>
                        }
                    </select>
                    <a href="decks" @onclick="Cancel">Manage decks</a>
                </div>
            }
        </div>
    </DialogContent>
    <DialogActions>
        <MudButton id="challenge-cancel" Variant="Variant.Text" Color="Color.Secondary" OnClick="Cancel">Cancel</MudButton>
        <MudButton id="send-challenge" Variant="Variant.Filled" Color="Color.Primary" Disabled="@(Chosen is null)" OnClick="Send">Send challenge</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public IReadOnlyList<StoredDeck> Decks { get; set; } = [];
    [Parameter] public MatchFormat Format { get; set; } = MatchFormat.Bo1;
    [Parameter] public string? DeckId { get; set; }

    private MatchFormat format;
    private string? deckId;

    protected override void OnInitialized()
    {
        format = Format;
        deckId = Decks.Any(d => d.Id == DeckId) ? DeckId : Decks.FirstOrDefault()?.Id;
    }

    private StoredDeck? Chosen => Decks.FirstOrDefault(d => d.Id == deckId);

    private void Cancel() => Dialog.Cancel();

    private void Send()
    {
        if (Chosen is { } deck) Dialog.Close(DialogResult.Ok(new ChallengeChoice(format, deck)));
    }
}
```

Create `src/CromoBound.Client/Dialogs/AcceptDialog.razor`:

```razor
<MudDialog>
    <DialogContent>
        <div class="cb-dialog-body">
            <p class="cb-sub">@Challenge.From challenges you to @Formats.InSentence(Challenge.Format). Pick the deck you play with.</p>
            @if (Decks.Count == 0)
            {
                <p class="cb-sub">You have no decks yet. <a href="decks" @onclick="NotNow">Add one on the decks page.</a></p>
            }
            else
            {
                <div class="cb-field">
                    <label class="cb-label" for="deck">Your deck</label>
                    <select id="deck" class="cb-input" @bind="deckId">
                        @foreach (var deck in Decks)
                        {
                            <option value="@deck.Id">@deck.Deck.Name</option>
                        }
                    </select>
                </div>
            }
        </div>
    </DialogContent>
    <DialogActions>
        <MudButton id="decline-in-dialog" Variant="Variant.Text" Color="Color.Secondary" OnClick="Decline">Decline</MudButton>
        <MudButton id="not-now" Variant="Variant.Text" Color="Color.Secondary" OnClick="NotNow">Not now</MudButton>
        <MudButton id="accept-and-play" Variant="Variant.Filled" Color="Color.Primary" Disabled="@(Chosen is null)" OnClick="Accept">Accept and play</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter, EditorRequired] public ChallengeInfo Challenge { get; set; } = default!;
    [Parameter] public IReadOnlyList<StoredDeck> Decks { get; set; } = [];
    [Parameter] public string? DeckId { get; set; }

    private string? deckId;

    protected override void OnInitialized() => deckId = Decks.Any(d => d.Id == DeckId) ? DeckId : Decks.FirstOrDefault()?.Id;

    private StoredDeck? Chosen => Decks.FirstOrDefault(d => d.Id == deckId);

    private void NotNow() => Dialog.Cancel();

    private void Decline() => Dialog.Close(DialogResult.Ok(new AcceptChoice(AcceptAnswer.Decline, null)));

    private void Accept()
    {
        if (Chosen is { } deck) Dialog.Close(DialogResult.Ok(new AcceptChoice(AcceptAnswer.Accept, deck)));
    }
}
```

Create `src/CromoBound.Client/Dialogs/DeckProblemsDialog.razor`:

```razor
<MudDialog>
    <DialogContent>
        <div class="cb-dialog-body cb-problems">
            <p class="cb-sub">The server refused <strong>@DeckName</strong>. It found:</p>
            <ul>
                @foreach (var problem in Problems)
                {
                    <li>@problem</li>
                }
            </ul>
        </div>
    </DialogContent>
    <DialogActions>
        <MudButton id="go-to-decks" Variant="Variant.Text" Color="Color.Secondary" OnClick="() => Answer(DeckProblemsAnswer.GoToDecks)">Go to decks</MudButton>
        <MudButton id="pick-another" Variant="Variant.Filled" Color="Color.Primary" OnClick="() => Answer(DeckProblemsAnswer.PickAnother)">Pick another deck</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public string DeckName { get; set; } = "";
    [Parameter] public IReadOnlyList<string> Problems { get; set; } = [];

    private void Answer(DeckProblemsAnswer answer) => Dialog.Close(DialogResult.Ok(answer));
}
```

Create `src/CromoBound.Client/Dialogs/AbandonedDialog.razor`:

```razor
<MudDialog>
    <DialogContent>
        <p class="cb-sub">The server was updated while your match was running, so it couldn't go on. Nobody wins it, and you're free to play again.</p>
    </DialogContent>
    <DialogActions>
        <MudButton id="abandoned-ok" Variant="Variant.Filled" Color="Color.Primary" OnClick="() => Dialog.Close()">OK</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
}
```

Replace `src/CromoBound.Client/Pages/Lobby.razor` with:

```razor
@page "/"
@implements IDisposable
@inject LobbyState State
@inject LobbySync Sync
@inject IGameHub Hub
@inject DeckStore Store
@inject SessionState Session
@inject IDialogService Dialogs
@inject ISnackbar Snackbar
@inject NavigationManager Nav

<main class="cb-main">
    <div class="cb-head">
        <div>
            <h1 class="cb-title">Play</h1>
            <p class="cb-sub">@Subtitle</p>
        </div>
        <MudButton Href="decks" Variant="Variant.Outlined" Color="Color.Secondary">Your decks (@deckCount)</MudButton>
    </div>
    @if (!State.Loaded)
    {
        <p class="cb-sub">Connecting to the lobby...</p>
    }
    else
    {
        <div class="cb-row">
            <section class="cb-col-main" aria-labelledby="players-title">
                <h2 id="players-title" class="cb-section-title">Players</h2>
                @if (State.Players.Count == 0)
                {
                    <p class="cb-sub">No one else has an account yet.</p>
                }
                <div class="cb-grid">
                    @foreach (var player in State.Players)
                    {
                        <div class="cb-panel cb-player @StatusClass(player)" data-player="@player.UserName">
                            <div class="cb-player-head">
                                <span class="cb-avatar-lg" aria-hidden="true">@player.UserName[..1].ToUpperInvariant()</span>
                                <div>
                                    <span class="cb-player-name">@player.UserName</span>
                                    <span class="cb-status"><span class="cb-dot" aria-hidden="true"></span>@Status(player)</span>
                                </div>
                            </div>
                            <MudButton Class="challenge" Variant="Variant.Filled" Color="Color.Primary" Disabled="@(!CanChallenge(player))"
                                       OnClick="() => ChallengeAsync(player.UserName)" aria-label="@($"Challenge {player.UserName}")">Challenge</MudButton>
                        </div>
                    }
                </div>
                @if (State.Sent is not null)
                {
                    <p class="cb-sub">You have an open challenge. Cancel it to challenge someone else.</p>
                }
            </section>
            <section class="cb-col-side" aria-labelledby="challenges-title">
                <h2 id="challenges-title" class="cb-section-title">Challenges</h2>
                @foreach (var challenge in State.Received)
                {
                    <div class="cb-panel highlight cb-received" data-from="@challenge.From">
                        <div class="cb-head">
                            <span class="cb-kicker accent">Received</span>
                            <span class="cb-chip">@Formats.Name(challenge.Format)</span>
                        </div>
                        <div><strong>@challenge.From</strong> challenges you</div>
                        <div class="cb-actions">
                            <MudButton Class="decline" Variant="Variant.Text" Color="Color.Secondary" Disabled="@(!CanAnswer)"
                                       OnClick="() => DeclineAsync(challenge)">Decline</MudButton>
                            <MudButton Class="accept" Variant="Variant.Filled" Color="Color.Primary" Disabled="@(!CanAccept)"
                                       OnClick="() => AcceptAsync(challenge)">Accept</MudButton>
                        </div>
                    </div>
                }
                <div class="cb-panel cb-sent">
                    <span class="cb-kicker">Sent</span>
                    @if (State.Sent is { } sent)
                    {
                        <div>You challenged <strong>@sent.To</strong> to @Formats.InSentence(sent.Format). Waiting for an answer.</div>
                        <div class="cb-actions">
                            <MudButton id="cancel-challenge" Variant="Variant.Text" Color="Color.Secondary" Disabled="@(!CanAnswer)"
                                       OnClick="() => CancelAsync(sent)">Cancel challenge</MudButton>
                        </div>
                    }
                    else
                    {
                        <p class="cb-sub">You haven't challenged anyone.</p>
                    }
                </div>
            </section>
        </div>
    }
</main>

@code {
    private int deckCount;
    private bool busy;

    private string Me => Session.Me?.UserName ?? "";

    private string Subtitle => State.Online switch
    {
        0 => "Pick an opponent and a deck. None of your friends are online right now.",
        1 => "Pick an opponent and a deck. 1 of your friends is online.",
        var n => $"Pick an opponent and a deck. {n} of your friends are online.",
    };

    private bool CanAnswer => State.IsConnected && !busy;
    private bool CanAccept => CanAnswer && !State.Maintenance && State.MatchId is null;
    private bool CanChallenge(PlayerPresence player) => CanAccept && State.Sent is null && !player.InMatch;

    private static string Status(PlayerPresence player) => player.InMatch ? "In a match" : player.Online ? "Online" : "Offline";
    private static string StatusClass(PlayerPresence player) => player.InMatch ? "busy" : player.Online ? "online" : "off";

    protected override async Task OnInitializedAsync()
    {
        State.Changed += OnChanged;
        GoToMatchIfPlaying();
        deckCount = (await Store.ListAsync()).Count;
    }

    private void OnChanged() => _ = InvokeAsync(() =>
    {
        GoToMatchIfPlaying();
        StateHasChanged();
    });

    /// <summary>A player in a match plays it (spec §6.3).</summary>
    private void GoToMatchIfPlaying()
    {
        if (State.MatchId is { } id) Nav.NavigateTo($"match/{id}");
    }

    private async Task ChallengeAsync(string opponent)
    {
        var (format, deckId) = (MatchFormat.Bo1, (string?)null);
        while (true)
        {
            var parameters = new DialogParameters<ChallengeDialog>
            {
                { d => d.Decks, await Store.ListAsync() }, { d => d.Format, format }, { d => d.DeckId, deckId },
            };
            var dialog = await Dialogs.ShowAsync<ChallengeDialog>($"Challenge {opponent}", parameters, DialogDefaults.Options);
            if (await dialog.Result is not { Canceled: false, Data: ChallengeChoice choice }) return;
            (format, deckId) = (choice.Format, choice.Deck.Id);
            var reply = await CallAsync(() => Hub.ChallengeAsync(opponent, choice.Format, choice.Deck.Deck));
            if (reply.Id is { } id)
            {
                State.ChallengeSent(new ChallengeInfo(id, Me, opponent, choice.Format));
                return;
            }
            if (!await PickAnotherAsync(reply, choice.Deck)) return;
        }
    }

    private async Task AcceptAsync(ChallengeInfo challenge)
    {
        string? deckId = null;
        while (true)
        {
            var parameters = new DialogParameters<AcceptDialog>
            {
                { d => d.Challenge, challenge }, { d => d.Decks, await Store.ListAsync() }, { d => d.DeckId, deckId },
            };
            var dialog = await Dialogs.ShowAsync<AcceptDialog>($"Play {challenge.From}?", parameters, DialogDefaults.Options);
            if (await dialog.Result is not { Canceled: false, Data: AcceptChoice choice }) return;
            if (choice.Answer == AcceptAnswer.Decline || choice.Deck is not { } deck)
            {
                await DeclineAsync(challenge);
                return;
            }
            deckId = deck.Id;
            var reply = await CallAsync(() => Hub.AcceptAsync(challenge.ChallengeId, deck.Deck));
            if (reply.Id is not null) return;
            if (!await PickAnotherAsync(reply, deck)) return;
        }
    }

    private async Task DeclineAsync(ChallengeInfo challenge) => await CallAsync(() => Hub.DeclineAsync(challenge.ChallengeId));

    private async Task CancelAsync(ChallengeInfo challenge) => await CallAsync(() => Hub.CancelAsync(challenge.ChallengeId));

    /// <summary>Runs one hub call with the buttons off. A refusal without deck problems is shown as it comes, and the lobby is
    /// reloaded, so a challenge that is gone disappears; deck problems are left to <see cref="PickAnotherAsync"/>.</summary>
    private async Task<HubReply> CallAsync(Func<Task<HubReply>> call)
    {
        busy = true;
        HubReply reply;
        try
        {
            reply = await call();
        }
        finally
        {
            busy = false;
        }
        if (reply.Error is { } error && reply.DeckIssues is not { Count: > 0 })
        {
            Snackbar.Add(error, Severity.Error);
            await Sync.ReloadAsync();
        }
        return reply;
    }

    /// <summary>Shows a refused deck's problems; true when the player wants to pick another deck.</summary>
    private async Task<bool> PickAnotherAsync(HubReply reply, StoredDeck deck)
    {
        if (reply.DeckIssues is not { Count: > 0 } issues) return false;
        var parameters = new DialogParameters<DeckProblemsDialog>
        {
            { d => d.DeckName, deck.Deck.Name }, { d => d.Problems, issues.Select(i => i.Message).ToList() },
        };
        var dialog = await Dialogs.ShowAsync<DeckProblemsDialog>("That deck isn't legal", parameters, DialogDefaults.Options);
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: DeckProblemsAnswer.PickAnother }) return true;
        if (result is { Canceled: false, Data: DeckProblemsAnswer.GoToDecks }) Nav.NavigateTo("decks");
        return false;
    }

    public void Dispose() => State.Changed -= OnChanged;
}
```

The page injects the lobby state as `State`: inside the class `Lobby`, no member can be named `Lobby`.

In `src/CromoBound.Client/Layout/MainLayout.razor`, show the abandoned dialog. Replace `OnLobbyChanged` with:

```csharp
    private void OnLobbyChanged() => _ = InvokeAsync(async () =>
    {
        StateHasChanged();
        if (Lobby.TakeAbandoned() is not null)
            await Dialogs.ShowAsync<AbandonedDialog>("Your match was stopped", DialogDefaults.Options);
    });
```

Add to `src/CromoBound.Client/wwwroot/css/app.css`:

```css
.cb-player.online .cb-avatar-lg { border-color: #2f8f75; }
.cb-player.online .cb-status { color: var(--cb-online-text); }
.cb-player.online .cb-dot { background: var(--cb-online); }
.cb-player.busy .cb-avatar-lg { border-color: #a8662a; }
.cb-player.busy .cb-status { color: var(--cb-busy-text); }
.cb-player.busy .cb-dot { background: var(--cb-busy); }
.cb-player.off .cb-avatar-lg { border-color: #3d2f30; }
.cb-player.off .cb-status { color: var(--cb-muted); }
.cb-player.off .cb-dot { background: var(--cb-off); }
.cb-received .cb-chip { color: var(--cb-accent-strong); }
.cb-dialog-body { display: flex; flex-direction: column; gap: 18px; }
.cb-fieldset { margin: 0; padding: 0; border: 0; display: flex; flex-direction: column; gap: 8px; }
.cb-problems ul { margin: 0; padding-left: 20px; display: flex; flex-direction: column; gap: 6px; }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Client tests/CromoBound.Client.Tests
git commit -m "feat(client): add the lobby with challenges and their dialogs"
```

---

### Task 6: The match page

**Files:**
- Create:
  - `src/CromoBound.Client/Pages/Match.razor`, `Dialogs/ConcedeDialog.razor`, `Dialogs/ResultDialog.razor`
  - `tests/CromoBound.Client.Tests/Views.cs`, `MatchPageTests.cs`
- Modify: `src/CromoBound.Client/wwwroot/css/app.css`

**Interfaces:**
- Consumes:
  - Task 3's `LobbyState` (`MatchId`, `Opponent`, `Seat`, `CurrentView`, `Ended`, `Loaded`, `IsConnected`, `Changed`), `IGameHub.SubmitAsync`, `Formats` and `GameConnection.Unexpected`.
  - Task 5's `AbandonedDialog`, `DialogDefaults` and `Ui.RenderDialogs()`.
  - `Concede` from `CromoBound.Engine.Actions`; `PlayerView` and `PlayerSideView`.
- Produces:
  - The page `/match/{Id:guid}`.
  - `ConcedeDialog` (`Message`).
  - `ResultDialog` (`Opponent`, `MyWins`, `TheirWins`).
  - In the tests: `Views.Of(...)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Client.Tests/Views.cs`:

```csharp
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Tests;

/// <summary>Player views with only what the match page reads: format, stage, game number and game wins.</summary>
internal static class Views
{
    public static PlayerView Of(
        MatchFormat format = MatchFormat.Bo3, MatchStage stage = MatchStage.Playing, int game = 1, int myWins = 0, int theirWins = 0, int seat = 0)
    {
        PlayerSideView Side(int index, int wins) => new(
            new PlayerId(index), 0, 0, wins, new PoolView(0, new Dictionary<Domain, int>(), 0), [], [], [], null, 0, 0, 0, [], [], null, 0);
        PlayerSideView[] sides = seat == 0 ? [Side(0, myWins), Side(1, theirWins)] : [Side(0, theirWins), Side(1, myWins)];
        return new PlayerView(new PlayerId(seat), format, stage, game, null, sides, null, [], [], [], null, null, []);
    }
}
```

Create `tests/CromoBound.Client.Tests/MatchPageTests.cs`:

```csharp
using Bunit;
using CromoBound.Client.Pages;
using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Engine;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;

namespace CromoBound.Client.Tests;

public class MatchPageTests
{
    private static readonly Guid M = Guid.Parse("00000000-0000-0000-0000-0000000000ff");

    private static Ui Playing(int game = 2, int myWins = 1, int theirWins = 0, MatchFormat format = MatchFormat.Bo3, int seat = 0)
    {
        var ui = new Ui();
        ui.Lobby.Load(new LobbyReply([], [], M, null, false),
            new MatchReply(M, Views.Of(format, MatchStage.Playing, game, myWins, theirWins, seat), "giulia"));
        ui.Lobby.SetConnection(HubState.Connected);
        return ui;
    }

    private static IRenderedComponent<Match> Render(Ui ui, Guid? id = null) => ui.Ctx.Render<Match>(ps => ps.Add(p => p.Id, id ?? M));

    [Fact]
    public async Task The_page_shows_the_players_format_stage_and_score()
    {
        await using var ui = Playing(seat: 1);

        var cut = Render(ui);

        cut.WaitForAssertion(() => Assert.Equal("marco vs giulia", cut.Find("h1").TextContent));
        Assert.Equal("Best of three · game 2", cut.Find("#match-format").TextContent);
        Assert.Equal("Playing", cut.Find("#match-stage").TextContent);
        Assert.Equal(("1", "0"), (cut.Find("#my-wins").TextContent, cut.Find("#their-wins").TextContent));
        Assert.Equal(3, cut.FindAll(".cb-pip").Count);
        Assert.Single(cut.FindAll(".cb-pip.on"));
        Assert.Contains("The board comes in the next update.", cut.Markup);
    }

    [Fact]
    public async Task A_match_that_isnt_yours_goes_back_to_the_lobby()
    {
        await using var ui = Playing();
        var other = Guid.NewGuid();
        ui.Nav.NavigateTo($"match/{other}");

        Render(ui, other);

        Assert.Equal("http://localhost/", ui.Nav.Uri);
    }

    [Fact]
    public async Task A_page_opened_before_the_lobby_loads_waits_for_it()
    {
        await using var ui = new Ui();
        ui.Nav.NavigateTo($"match/{M}");
        var cut = Render(ui);
        Assert.Contains("Loading the match...", cut.Markup);
        Assert.Equal($"http://localhost/match/{M}", ui.Nav.Uri);

        await cut.InvokeAsync(() => ui.Lobby.Load(new LobbyReply([], [], null, null, false), null));

        cut.WaitForAssertion(() => Assert.Equal("http://localhost/", ui.Nav.Uri));
    }

    [Fact]
    public async Task A_view_notice_updates_the_score()
    {
        await using var ui = Playing();
        var cut = Render(ui);

        await cut.InvokeAsync(() => ui.Lobby.View(new MatchViewNotice(M, Views.Of(game: 3, myWins: 1, theirWins: 1, stage: MatchStage.Mulligan))));

        cut.WaitForAssertion(() => Assert.Equal("1", cut.Find("#their-wins").TextContent));
        Assert.Equal("Best of three · game 3", cut.Find("#match-format").TextContent);
        Assert.Equal("Mulligan", cut.Find("#match-stage").TextContent);
    }

    [Fact]
    public async Task Conceding_asks_first_then_submits()
    {
        await using var ui = Playing();
        var dialogs = ui.RenderDialogs();
        var cut = Render(ui);

        await cut.ClickAsync("#concede");
        dialogs.WaitForAssertion(() => Assert.Contains(
            "giulia wins game 2. In a best of three, the match goes on to the next game unless this one decides it.", dialogs.Markup));
        await dialogs.ClickAsync("#keep-playing");
        Assert.Empty(ui.Hub.Calls);

        await cut.ClickAsync("#concede");
        await dialogs.ClickAsync("#confirm-concede");

        cut.WaitForAssertion(() => Assert.Equal(new[] { $"Submit {M} Concede" }, ui.Hub.Calls));
        Assert.IsType<Concede>(Assert.Single(ui.Hub.Actions));
    }

    [Fact]
    public async Task In_a_best_of_one_conceding_gives_the_match()
    {
        await using var ui = Playing(game: 1, myWins: 0, format: MatchFormat.Bo1);
        var dialogs = ui.RenderDialogs();
        var cut = Render(ui);

        await cut.ClickAsync("#concede");

        dialogs.WaitForAssertion(() => Assert.Contains("giulia wins the match.", dialogs.Markup));
    }

    [Fact]
    public async Task A_refused_concede_shows_the_engines_reason()
    {
        await using var ui = Playing();
        ui.Hub.Submitted = new SubmitReply(false, new Rejection(RejectionCode.MatchOver, "The match is over."), null);
        var dialogs = ui.RenderDialogs();
        var cut = Render(ui);

        await cut.ClickAsync("#concede");
        await dialogs.ClickAsync("#confirm-concede");

        cut.WaitForAssertion(() => Assert.Contains("The match is over.", ui.Notices));
    }

    [Theory]
    [InlineData("giulia", 0, "giulia wins the match", 1, 2)]
    [InlineData("marco", 1, "You win the match", 2, 1)]
    public async Task The_end_of_the_match_names_the_winner_and_leads_back(string winner, int seat, string title, int mine, int theirs)
    {
        await using var ui = Playing(seat: seat);
        ui.Nav.NavigateTo($"match/{M}");
        var dialogs = ui.RenderDialogs();
        var cut = Render(ui);
        var wins = new int[2];
        wins[seat] = mine;
        wins[1 - seat] = theirs;

        await cut.InvokeAsync(() => ui.Lobby.MatchEnded(new MatchEndedNotice(M, MatchEndReason.Finished, wins, winner)));

        dialogs.WaitForAssertion(() => Assert.Equal(title, dialogs.Find(".mud-dialog-title").TextContent.Trim()));
        Assert.Equal(($"{mine}", $"{theirs}"), (dialogs.Find("#result-mine").TextContent, dialogs.Find("#result-theirs").TextContent));
        Assert.Equal($"http://localhost/match/{M}", ui.Nav.Uri);
        await dialogs.ClickAsync("#back-to-lobby");
        cut.WaitForAssertion(() => Assert.Equal("http://localhost/", ui.Nav.Uri));
    }

    [Fact]
    public async Task The_page_is_disabled_while_reconnecting()
    {
        await using var ui = Playing();
        ui.Lobby.SetConnection(HubState.Reconnecting);

        var cut = Render(ui);

        cut.WaitForAssertion(() => Assert.True(cut.Find("#concede").HasAttribute("disabled")));
    }
}
```

bUnit's navigation manager starts at `http://localhost/`. So the tests that check a move to the lobby first navigate to the match page, as a player would arrive there.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (the page `Match` and its dialogs don't exist).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Client/Dialogs/ConcedeDialog.razor`:

```razor
<MudDialog>
    <DialogContent>
        <p class="cb-sub">@Message</p>
    </DialogContent>
    <DialogActions>
        <MudButton id="keep-playing" Variant="Variant.Text" Color="Color.Secondary" OnClick="() => Dialog.Cancel()">Keep playing</MudButton>
        <MudButton id="confirm-concede" Variant="Variant.Outlined" Class="cb-danger-button" OnClick="() => Dialog.Close(DialogResult.Ok(true))">Concede</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public string Message { get; set; } = "";
}
```

Create `src/CromoBound.Client/Dialogs/ResultDialog.razor`:

```razor
<MudDialog>
    <DialogContent>
        <div class="cb-result" role="group" aria-label="Games won">
            <div><span>You</span><b id="result-mine">@MyWins</b></div>
            <span class="cb-pip" aria-hidden="true"></span>
            <div><span>@Opponent</span><b id="result-theirs">@TheirWins</b></div>
        </div>
    </DialogContent>
    <DialogActions>
        <MudButton id="back-to-lobby" Variant="Variant.Filled" Color="Color.Primary" OnClick="() => Dialog.Close()">Back to the lobby</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Parameter] public string Opponent { get; set; } = "";
    [Parameter] public int MyWins { get; set; }
    [Parameter] public int TheirWins { get; set; }
}
```

Create `src/CromoBound.Client/Pages/Match.razor`:

```razor
@page "/match/{Id:guid}"
@implements IDisposable
@using CromoBound.Engine.Actions
@using CromoBound.Engine.Views
@inject LobbyState Lobby
@inject IGameHub Hub
@inject SessionState Session
@inject IDialogService Dialogs
@inject ISnackbar Snackbar
@inject NavigationManager Nav

<main class="cb-main narrow">
    @if (!Lobby.Loaded || (Lobby.MatchId != Id && Lobby.Ended?.MatchId != Id))
    {
        <p class="cb-sub">Loading the match...</p>
    }
    else
    {
        <section class="cb-panel cb-match" aria-labelledby="match-title">
            <div class="cb-head">
                <div>
                    <h1 id="match-title" class="cb-title">@Me vs @Opponent</h1>
                    <span id="match-format" class="cb-sub">@FormatLine</span>
                </div>
                @if (Lobby.CurrentView is { } stageView)
                {
                    <span id="match-stage" class="cb-stage">@Formats.Stage(stageView.Stage)</span>
                }
            </div>
            @if (Lobby.CurrentView is { } view)
            {
                <div class="cb-score" role="group" aria-label="Games won">
                    <div><span>You</span><b id="my-wins" class="mine">@Wins(view, mine: true)</b></div>
                    <div class="cb-pips" aria-hidden="true">
                        @for (var game = 0; game < Games(view); game++)
                        {
                            <span class="cb-pip @(game < Wins(view, true) + Wins(view, false) ? "on" : "")"></span>
                        }
                    </div>
                    <div><span>@Opponent</span><b id="their-wins">@Wins(view, mine: false)</b></div>
                </div>
            }
            else
            {
                <p class="cb-sub">Waiting for the match to begin...</p>
            }
            <div class="cb-placeholder">The board comes in the next update. For now you can follow the score here and concede.</div>
            <div class="cb-actions">
                <MudButton id="concede" Variant="Variant.Outlined" Class="cb-danger-button" Disabled="@(!CanConcede)" OnClick="ConcedeAsync">Concede this game</MudButton>
            </div>
        </section>
    }
</main>

@code {
    [Parameter] public Guid Id { get; set; }

    private bool busy;
    private bool resultShown;

    private string Me => Session.Me?.UserName ?? "";
    private string Opponent => Lobby.Opponent ?? "your opponent";

    private string FormatLine => Lobby.CurrentView is { } view ? $"{Formats.Name(view.Format)} · game {view.GameNumber}" : "";

    private bool CanConcede => Lobby.IsConnected && Lobby.MatchId == Id && Lobby.CurrentView is { Stage: not MatchStage.Over } && !busy;

    private static int Games(PlayerView view) => view.Format == MatchFormat.Bo3 ? 3 : 1;

    private static int Wins(PlayerView view, bool mine) => view.Players[mine ? view.Viewer.Index : 1 - view.Viewer.Index].GameWins;

    protected override void OnInitialized() => Lobby.Changed += OnChanged;

    protected override void OnParametersSet() => Check();

    private void OnChanged() => _ = InvokeAsync(() =>
    {
        Check();
        StateHasChanged();
    });

    /// <summary>The end of this match shows its result, once; a match id that isn't the player's running match goes back to the
    /// lobby (spec §8). Before the lobby has loaded there is nothing to compare with, so the page waits.</summary>
    private void Check()
    {
        if (!Lobby.Loaded) return;
        if (Lobby.Ended is { } ended && ended.MatchId == Id)
        {
            if (resultShown) return;
            resultShown = true;
            _ = ShowEndThenLeaveAsync(ended);
            return;
        }
        if (Lobby.MatchId != Id) Nav.NavigateTo("");
    }

    /// <summary>Not awaited by <see cref="Check"/>: the dialog stays open until the player closes it, and the page keeps rendering
    /// meanwhile.</summary>
    private async Task ShowEndThenLeaveAsync(MatchEndedNotice ended)
    {
        await ShowEndAsync(ended);
        Nav.NavigateTo("");
    }

    private async Task ShowEndAsync(MatchEndedNotice ended)
    {
        if (ended.Reason == MatchEndReason.Abandoned)
        {
            await (await Dialogs.ShowAsync<AbandonedDialog>("Your match was stopped", DialogDefaults.Options)).Result;
            return;
        }
        var seat = Lobby.Seat?.Index ?? 0;
        var title = string.Equals(ended.Winner, Me, StringComparison.OrdinalIgnoreCase) ? "You win the match" : $"{ended.Winner} wins the match";
        var parameters = new DialogParameters<ResultDialog>
        {
            { d => d.Opponent, Opponent }, { d => d.MyWins, ended.GameWins[seat] }, { d => d.TheirWins, ended.GameWins[1 - seat] },
        };
        await (await Dialogs.ShowAsync<ResultDialog>(title, parameters, DialogDefaults.Options)).Result;
    }

    private async Task ConcedeAsync()
    {
        if (Lobby.CurrentView is not { } view) return;
        var message = view.Format == MatchFormat.Bo3
            ? $"{Opponent} wins game {view.GameNumber}. In a best of three, the match goes on to the next game unless this one decides it."
            : $"{Opponent} wins the match.";
        var dialog = await Dialogs.ShowAsync<ConcedeDialog>("Concede this game?",
            new DialogParameters<ConcedeDialog> { { d => d.Message, message } }, DialogDefaults.Options);
        if (await dialog.Result is not { Canceled: false }) return;
        busy = true;
        try
        {
            var reply = await Hub.SubmitAsync(Id, new Concede());
            if (!reply.Accepted) Snackbar.Add(reply.Rejection?.Message ?? reply.Error ?? GameConnection.Unexpected, Severity.Error);
        }
        finally
        {
            busy = false;
        }
    }

    public void Dispose() => Lobby.Changed -= OnChanged;
}
```

Add to `src/CromoBound.Client/wwwroot/css/app.css`:

```css
.cb-match { padding: 28px; gap: 28px; }
.cb-stage { padding: 6px 14px; border-radius: 999px; background: rgba(224,74,58,.14); border: 1px solid #b8463a; color: var(--cb-accent-strong); font-weight: 600; letter-spacing: .06em; font-size: 13px; text-transform: uppercase; }
.cb-score span { font-size: 15px; color: var(--cb-soft); }
.cb-score b { color: var(--cb-soft); }
.cb-score b.mine { color: var(--cb-accent-strong); }
.cb-pips { flex-direction: row !important; justify-content: center; gap: 10px !important; }
.cb-pip { width: 14px; height: 14px; transform: rotate(45deg); border: 2px solid var(--cb-accent-edge); box-sizing: border-box; }
.cb-pip.on { background: var(--cb-primary); border-color: var(--cb-primary); }
.cb-result { display: flex; align-items: center; justify-content: center; gap: 28px; padding: 8px 0; }
.cb-result div { display: flex; flex-direction: column; align-items: center; gap: 2px; }
.cb-result span { font-size: 14px; color: var(--cb-muted); }
.cb-result b { font-weight: 700; font-size: 52px; line-height: 1; color: var(--cb-soft); }
.cb-result .cb-pip { background: var(--cb-accent-edge); border: 0; width: 12px; height: 12px; }
.cb-danger-button { color: var(--cb-danger) !important; border-color: #7a2a3e !important; }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Client tests/CromoBound.Client.Tests
git commit -m "feat(client): add the match page with concede and the result"
```

---

### Task 7: The admin pages and the manual checks

**Files:**
- Create:
  - `src/CromoBound.Client/Layout/AdminTabs.razor`, `Pages/Admin/Users.razor`, `Pages/Admin/Maintenance.razor`
  - `src/CromoBound.Client/Dialogs/CreateUserDialog.razor`, `Dialogs/SetPasswordDialog.razor`
  - `tests/CromoBound.Client.Tests/AdminPagesTests.cs`
  - `docs/client-manual-checks.md`
- Modify: `src/CromoBound.Client/wwwroot/css/app.css`

**Interfaces:**
- Consumes:
  - Task 2's `IServerApi` (`UsersAsync`, `CreateUserAsync`, `SetPasswordAsync`, `SetAdminAsync`, `SetDisabledAsync`, `MaintenanceAsync`, `SetMaintenanceAsync`), `ServerApi.Forbidden`, `SessionState` and `FakeServerApi`.
  - Task 3's `LobbyState.PlayersChanged`, `Ui.Notices`.
  - Task 5's `DialogDefaults` and `Ui.RenderDialogs()`.
- Produces:
  - The pages `/admin/users` and `/admin/maintenance`.
  - `CreateUserDialog` (returns the new `UserSummary`) and `SetPasswordDialog` (`User`).
  - `AdminTabs`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Client.Tests/AdminPagesTests.cs`:

```csharp
using Bunit;
using CromoBound.Client.Pages.Admin;
using CromoBound.Client.Services;
using CromoBound.Contracts;

namespace CromoBound.Client.Tests;

public class AdminPagesTests
{
    private static Ui Admin()
    {
        var ui = new Ui(new MeResponse("marco", true));
        ui.Api.Users.AddRange([new(1, "admin", true, false), new(2, "giulia", false, false), new(3, "marco", true, false), new(4, "tommaso", false, true)]);
        return ui;
    }

    private static string Row(IRenderedComponent<Users> cut, string user) =>
        string.Join("|", cut.FindAll($"[data-user='{user}'] td").Take(3).Select(td => td.TextContent.Trim()));

    [Fact]
    public async Task Users_are_listed_with_their_role_and_account()
    {
        await using var ui = Admin();

        var cut = ui.Ctx.Render<Users>();

        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr").Count));
        Assert.Equal("admin|Admin|Active", Row(cut, "admin"));
        Assert.Equal("giulia|Player|Active", Row(cut, "giulia"));
        Assert.Equal("marco (you)|Admin|Active", Row(cut, "marco"));
        Assert.Equal("tommaso|Player|Disabled", Row(cut, "tommaso"));
        Assert.Equal(new[] { "Set password", "Make admin", "Enable" },
            cut.FindAll("[data-user='tommaso'] button").Select(b => b.TextContent.Trim()));
    }

    [Fact]
    public async Task A_player_gets_a_plain_refusal()
    {
        await using var ui = new Ui();

        var cut = ui.Ctx.Render<Users>();

        Assert.Equal(ServerApi.Forbidden, cut.Find("[role=alert]").TextContent);
        Assert.Empty(cut.FindAll("table"));
    }

    [Fact]
    public async Task Switching_the_role_and_the_account_calls_the_server_and_reloads()
    {
        await using var ui = Admin();
        var cut = ui.Ctx.Render<Users>();
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr").Count));

        await cut.ClickAsync("[data-user='giulia'] .toggle-admin");
        await cut.ClickAsync("[data-user='giulia'] .toggle-disabled");

        cut.WaitForAssertion(() => Assert.Equal("giulia|Admin|Disabled", Row(cut, "giulia")));
        Assert.Equal(new[] { "admin 2 True", "disabled 2 True" }, ui.Api.Calls);
    }

    [Fact]
    public async Task A_refused_change_shows_the_servers_message()
    {
        await using var ui = Admin();
        ui.Api.NextError = "You can't do that to your own account.";
        var cut = ui.Ctx.Render<Users>();
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr").Count));

        await cut.ClickAsync("[data-user='marco'] .toggle-disabled");

        cut.WaitForAssertion(() => Assert.Contains("You can't do that to your own account.", ui.Notices));
        Assert.Equal("marco (you)|Admin|Active", Row(cut, "marco"));
    }

    [Fact]
    public async Task Creating_a_user_keeps_the_dialog_open_on_a_refusal()
    {
        await using var ui = Admin();
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Users>();
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr").Count));
        ui.Api.NextError = "A password has at least 12 characters.";

        await cut.ClickAsync("#create-user");
        dialogs.WaitForElement("#new-username").Input("chiara");
        dialogs.Find("#new-password").Input("short");
        await dialogs.InvokeAsync(() => dialogs.Find("#new-admin").Change(true));
        await dialogs.ClickAsync("#create");
        dialogs.WaitForAssertion(() => Assert.Equal("A password has at least 12 characters.", dialogs.Find("[role=alert]").TextContent));
        Assert.Equal("chiara", dialogs.Find("#new-username").GetAttribute("value"));

        dialogs.Find("#new-password").Input("a-long-enough-password");
        await dialogs.ClickAsync("#create");

        cut.WaitForAssertion(() => Assert.Equal("chiara|Admin|Active", Row(cut, "chiara")));
        Assert.Empty(dialogs.FindAll("#create"));
        Assert.Equal(new[] { "create chiara True", "create chiara True" }, ui.Api.Calls);
    }

    [Fact]
    public async Task Setting_a_password_confirms_it()
    {
        await using var ui = Admin();
        var dialogs = ui.RenderDialogs();
        var cut = ui.Ctx.Render<Users>();
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr").Count));

        await cut.ClickAsync("[data-user='giulia'] .set-password");
        dialogs.WaitForElement("#password-input").Input("a-long-enough-password");
        await dialogs.ClickAsync("#save-password");

        cut.WaitForAssertion(() => Assert.Contains("giulia's password is set.", ui.Notices));
        Assert.Equal(new[] { "password 2" }, ui.Api.Calls);
    }

    [Fact]
    public async Task The_maintenance_page_switches_and_counts_running_matches_live()
    {
        await using var ui = Admin();
        ui.Api.Maintenance = new MaintenanceStatus(false, 2);
        var cut = ui.Ctx.Render<Maintenance>();
        cut.WaitForAssertion(() => Assert.Equal("2", cut.Find("#running").TextContent));
        Assert.False(cut.Find("#maintenance-switch").HasAttribute("checked"));

        await cut.InvokeAsync(() => cut.Find("#maintenance-switch").Change(true));
        cut.WaitForAssertion(() => Assert.True(cut.Find("#maintenance-switch").HasAttribute("checked")));
        Assert.Equal(new[] { "maintenance True" }, ui.Api.Calls);

        ui.Api.Maintenance = ui.Api.Maintenance with { RunningMatches = 0 };
        await cut.InvokeAsync(() => ui.Lobby.PlayerChanged(new PlayerPresence("giulia", true, false)));
        cut.WaitForAssertion(() => Assert.Equal("0", cut.Find("#running").TextContent));
    }

    [Fact]
    public async Task A_refused_switch_stays_as_it_was()
    {
        await using var ui = Admin();
        ui.Api.NextError = ServerApi.Unexpected;
        var cut = ui.Ctx.Render<Maintenance>();
        cut.WaitForAssertion(() => Assert.Equal("0", cut.Find("#running").TextContent));

        await cut.InvokeAsync(() => cut.Find("#maintenance-switch").Change(true));

        cut.WaitForAssertion(() => Assert.Contains(ServerApi.Unexpected, ui.Notices));
        Assert.False(cut.Find("#maintenance-switch").HasAttribute("checked"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (the admin pages don't exist).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Client/Layout/AdminTabs.razor`:

```razor
<nav class="cb-tabs" aria-label="Admin">
    <NavLink href="admin/users">Users</NavLink>
    <NavLink href="admin/maintenance">Maintenance</NavLink>
</nav>
```

Create `src/CromoBound.Client/Dialogs/CreateUserDialog.razor`:

```razor
<MudDialog>
    <DialogContent>
        <form class="cb-dialog-body" @onsubmit="CreateAsync" @onsubmit:preventDefault>
            <div class="cb-field">
                <label class="cb-label" for="new-username">Username</label>
                <input id="new-username" class="cb-input" autocomplete="off" @bind="userName" @bind:event="oninput" />
                <span class="cb-sub">3 to 24 letters, digits, _ or -</span>
            </div>
            <div class="cb-field">
                <label class="cb-label" for="new-password">Password</label>
                <input id="new-password" class="cb-input @(error is null ? "" : "invalid")" type="password" autocomplete="new-password"
                       aria-describedby="create-error" @bind="password" @bind:event="oninput" />
            </div>
            <label class="cb-check">
                <input id="new-admin" type="checkbox" @bind="isAdmin" />
                Admin (can manage users and maintenance)
            </label>
            @if (error is not null)
            {
                <p id="create-error" class="cb-error" role="alert">@error</p>
            }
        </form>
    </DialogContent>
    <DialogActions>
        <MudButton id="create-cancel" Variant="Variant.Text" Color="Color.Secondary" OnClick="() => Dialog.Cancel()">Cancel</MudButton>
        <MudButton id="create" Variant="Variant.Filled" Color="Color.Primary" Disabled="busy" OnClick="CreateAsync">Create</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Inject] private IServerApi Api { get; set; } = default!;

    private string userName = "";
    private string password = "";
    private bool isAdmin;
    private string? error;
    private bool busy;

    /// <summary>The server checks the name and password; its refusal shows here and keeps what was typed (deviation 5).</summary>
    private async Task CreateAsync()
    {
        busy = true;
        var created = await Api.CreateUserAsync(new CreateUserRequest(userName.Trim(), password, isAdmin));
        busy = false;
        error = created.Error;
        if (created.Value is { } user) Dialog.Close(DialogResult.Ok(user));
    }
}
```

Create `src/CromoBound.Client/Dialogs/SetPasswordDialog.razor`:

```razor
<MudDialog>
    <DialogContent>
        <form class="cb-dialog-body" @onsubmit="SaveAsync" @onsubmit:preventDefault>
            <div class="cb-field">
                <label class="cb-label" for="password-input">New password for @User.UserName</label>
                <input id="password-input" class="cb-input @(error is null ? "" : "invalid")" type="password" autocomplete="new-password"
                       aria-describedby="password-error" @bind="password" @bind:event="oninput" />
                <span class="cb-sub">At least 12 characters. It stays until an admin changes it.</span>
            </div>
            @if (error is not null)
            {
                <p id="password-error" class="cb-error" role="alert">@error</p>
            }
        </form>
    </DialogContent>
    <DialogActions>
        <MudButton id="password-cancel" Variant="Variant.Text" Color="Color.Secondary" OnClick="() => Dialog.Cancel()">Cancel</MudButton>
        <MudButton id="save-password" Variant="Variant.Filled" Color="Color.Primary" Disabled="busy" OnClick="SaveAsync">Set password</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [CascadingParameter] private IMudDialogInstance Dialog { get; set; } = default!;
    [Inject] private IServerApi Api { get; set; } = default!;
    [Parameter, EditorRequired] public UserSummary User { get; set; } = default!;

    private string password = "";
    private string? error;
    private bool busy;

    private async Task SaveAsync()
    {
        busy = true;
        var result = await Api.SetPasswordAsync(User.Id, password);
        busy = false;
        error = result.Error;
        if (result.Ok) Dialog.Close(DialogResult.Ok(true));
    }
}
```

Create `src/CromoBound.Client/Pages/Admin/Users.razor`:

```razor
@page "/admin/users"
@inject IServerApi Api
@inject SessionState Session
@inject IDialogService Dialogs
@inject ISnackbar Snackbar

<main class="cb-main">
    <AdminTabs />
    @if (Session.Me?.CanManageUsers != true)
    {
        <p class="cb-error" role="alert">@ServerApi.Forbidden</p>
    }
    else
    {
        <section class="cb-panel" aria-labelledby="users-title">
            <div class="cb-head">
                <h1 id="users-title" class="cb-title">Users</h1>
                <MudButton id="create-user" Variant="Variant.Filled" Color="Color.Primary" OnClick="CreateAsync">Create user</MudButton>
            </div>
            @if (error is not null)
            {
                <p class="cb-error" role="alert">@error</p>
            }
            <table class="cb-table">
                <thead>
                    <tr><th scope="col">Name</th><th scope="col">Role</th><th scope="col">Account</th><th scope="col">Actions</th></tr>
                </thead>
                <tbody>
                    @foreach (var user in users)
                    {
                        <tr data-user="@user.UserName">
                            <td>@(IsMe(user) ? $"{user.UserName} (you)" : user.UserName)</td>
                            <td><span class="cb-chip @(user.IsAdmin ? "admin" : "")">@(user.IsAdmin ? "Admin" : "Player")</span></td>
                            <td class="@(user.Disabled ? "cb-danger-text" : "")">@(user.Disabled ? "Disabled" : "Active")</td>
                            <td>
                                <div class="cb-actions start">
                                    <MudButton Class="set-password" Variant="Variant.Text" Color="Color.Secondary" OnClick="() => SetPasswordAsync(user)">Set password</MudButton>
                                    <MudButton Class="toggle-admin" Variant="Variant.Text" Color="Color.Secondary"
                                               OnClick="() => ChangeAsync(() => Api.SetAdminAsync(user.Id, !user.IsAdmin))">@(user.IsAdmin ? "Remove admin" : "Make admin")</MudButton>
                                    <MudButton Class="@(user.Disabled ? "toggle-disabled" : "toggle-disabled cb-danger-text")" Variant="Variant.Text" Color="Color.Secondary"
                                               OnClick="() => ChangeAsync(() => Api.SetDisabledAsync(user.Id, !user.Disabled))">@(user.Disabled ? "Enable" : "Disable")</MudButton>
                                </div>
                            </td>
                        </tr>
                    }
                </tbody>
            </table>
        </section>
    }
</main>

@code {
    private IReadOnlyList<UserSummary> users = [];
    private string? error;

    private bool IsMe(UserSummary user) => string.Equals(user.UserName, Session.Me?.UserName, StringComparison.OrdinalIgnoreCase);

    protected override async Task OnInitializedAsync()
    {
        if (Session.Me?.CanManageUsers == true) await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var loaded = await Api.UsersAsync();
        error = loaded.Error;
        users = [.. (loaded.Value ?? []).OrderBy(u => u.UserName, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>A refusal pops up as it comes ("The last admin can't be removed."); the list is reloaded either way.</summary>
    private async Task ChangeAsync(Func<Task<ApiResult>> change)
    {
        var result = await change();
        if (result.Error is { } refusal) Snackbar.Add(refusal, Severity.Error);
        await LoadAsync();
    }

    private async Task CreateAsync()
    {
        var dialog = await Dialogs.ShowAsync<CreateUserDialog>("Create user", DialogDefaults.Options);
        if (await dialog.Result is { Canceled: false, Data: UserSummary created })
        {
            Snackbar.Add($"{created.UserName} can sign in now.", Severity.Success);
            await LoadAsync();
        }
    }

    private async Task SetPasswordAsync(UserSummary user)
    {
        var dialog = await Dialogs.ShowAsync<SetPasswordDialog>("Set password",
            new DialogParameters<SetPasswordDialog> { { d => d.User, user } }, DialogDefaults.Options);
        if (await dialog.Result is { Canceled: false }) Snackbar.Add($"{user.UserName}'s password is set.", Severity.Success);
    }
}
```

Create `src/CromoBound.Client/Pages/Admin/Maintenance.razor`:

```razor
@page "/admin/maintenance"
@implements IDisposable
@inject IServerApi Api
@inject SessionState Session
@inject LobbyState Lobby
@inject ISnackbar Snackbar

<main class="cb-main narrow">
    <AdminTabs />
    @if (Session.Me?.CanManageUsers != true)
    {
        <p class="cb-error" role="alert">@ServerApi.Forbidden</p>
    }
    else
    {
        <section class="cb-panel cb-maintenance" aria-labelledby="maintenance-title">
            <h1 id="maintenance-title" class="cb-title">Maintenance</h1>
            <label class="cb-switch-row">
                <span class="cb-switch-text">
                    <span class="cb-switch-title">Maintenance mode</span>
                    <span class="cb-sub">While it's on, no one can start a challenge or a match. Running matches go on.</span>
                </span>
                <input @key="version" id="maintenance-switch" type="checkbox" role="switch" class="cb-switch"
                       checked="@status.On" disabled="@busy" @onchange="ToggleAsync" />
            </label>
            <div class="cb-running">
                <div id="running" class="cb-big">@status.RunningMatches</div>
                <div class="cb-switch-text">
                    <span class="cb-switch-title">running matches</span>
                    <span class="cb-sub">Updates live. Deploy when this reaches 0: a match still running when the new version starts is abandoned.</span>
                </div>
            </div>
        </section>
    }
</main>

@code {
    private MaintenanceStatus status = new(false, 0);
    private bool busy;

    /// <summary>Bumped to rebuild the switch, so a refused change shows the switch as it really is.</summary>
    private int version;

    protected override async Task OnInitializedAsync()
    {
        if (Session.Me?.CanManageUsers != true) return;
        Lobby.PlayersChanged += OnPlayersChanged;
        await LoadAsync();
    }

    /// <summary>Matches start and end with presence changes, and maintenance switches with its notice (spec §9).</summary>
    private void OnPlayersChanged() => _ = InvokeAsync(async () =>
    {
        await LoadAsync();
        StateHasChanged();
    });

    private async Task LoadAsync()
    {
        if ((await Api.MaintenanceAsync()).Value is { } loaded) status = loaded;
    }

    private async Task ToggleAsync()
    {
        busy = true;
        var result = await Api.SetMaintenanceAsync(!status.On);
        busy = false;
        if (result.Value is { } changed) status = changed;
        else Snackbar.Add(result.Error ?? ServerApi.Unexpected, Severity.Error);
        version++;
    }

    public void Dispose() => Lobby.PlayersChanged -= OnPlayersChanged;
}
```

Add to `src/CromoBound.Client/wwwroot/css/app.css`:

```css
.cb-table th { text-transform: uppercase; }
.cb-actions.start { justify-content: flex-start; }
.cb-chip.admin { color: var(--cb-accent-strong); background: rgba(224,74,58,.14); border-color: #b8463a; }
.cb-maintenance { padding: 28px; gap: 24px; }
.cb-switch-row { display: flex; align-items: center; justify-content: space-between; gap: 20px; padding: 16px 20px; border-radius: 20px; background: var(--cb-raised); cursor: pointer; }
.cb-switch-text { display: flex; flex-direction: column; gap: 4px; }
.cb-switch-title { font-weight: 700; font-size: 17px; }
.cb-switch { appearance: none; width: 52px; height: 30px; border-radius: 999px; background: var(--cb-raised); border: 1px solid var(--cb-line); position: relative; cursor: pointer; flex: none; margin: 0; }
.cb-switch::after { content: ""; position: absolute; top: 3px; left: 3px; width: 22px; height: 22px; border-radius: 50%; background: var(--cb-label); transition: left .15s; }
.cb-switch:checked { background: var(--cb-primary); border-color: var(--cb-accent-strong); }
.cb-switch:checked::after { left: 25px; background: #fff; }
.cb-switch:focus-visible { outline: 2px solid var(--cb-accent); outline-offset: 2px; }
.cb-running { display: flex; align-items: center; gap: 20px; }
```

Create `docs/client-manual-checks.md`:

```markdown
# Client manual checks (Phase 4a)

Run these against a local server (`dotnet run --project src/CromoBound.Server`) before deploying a client change. The automated tests
cover each page against fakes; these checks cover the real browser, the real hub and the real cookie.

You need two accounts (an admin and a player) and two browsers, or one normal and one private window.

## Signing in
- [ ] Opening `/` signed out shows only the login page; `/decks`, `/match/<any id>` and `/_framework/blazor.webassembly.js` do too.
- [ ] After signing in, the top bar shows the logo, Play, Decks, the username and Sign out; the admin also sees Admin.
- [ ] Sign out goes to the login page, and the back button doesn't show the app again.

## Decks
- [ ] Pasting a deck JSON from `schema/deck.schema.json`'s format adds a deck tile with its counts.
- [ ] Pasting `{` shows "That isn't a deck the app can read: it isn't valid JSON." and adds nothing.
- [ ] Uploading a `.json` file fills the box; Add deck adds it.
- [ ] Rename and Delete work, and survive a reload.
- [ ] Signed in as the other account in the same browser, the decks list is empty.

## Lobby
- [ ] Each account sees the other, Online, with a green dot; closing the other browser turns it Offline.
- [ ] Challenging with a deck that isn't legal shows the server's problems; Pick another deck reopens the challenge.
- [ ] A legal challenge shows under Sent; the other browser pops up "<name> challenges you to a best of ..." and shows it under Received.
- [ ] Cancel challenge clears both sides, and the other browser pops up "<name> cancelled their challenge."
- [ ] Decline clears both sides, and the challenger pops up "<name> declined your challenge."
- [ ] Accept and play opens the match page in both browsers.

## Match
- [ ] The page shows "<you> vs <them>", the format and game, the stage and the score.
- [ ] Reloading the page shows the same.
- [ ] Concede asks first; Keep playing does nothing; Concede in a best of one shows the result dialog in both browsers, and "Back to the lobby" goes to the lobby.
- [ ] Opening `/match/<another id>` goes to the lobby.

## Connection and session
- [ ] Stopping the server shows "Reconnecting..." and disables Challenge and Accept; starting it again clears the banner and the lobby is current.
- [ ] Disabling the player's account from the admin's browser sends the player's browser to the login page.
- [ ] Restarting the server during a match shows "Your match was stopped" once, in the lobby.

## Admin
- [ ] Users lists every account with role and account state; creating one with a short password shows the server's message in the dialog.
- [ ] Make admin, Remove admin, Disable and Enable update the row; disabling your own account pops up "You can't do that to your own account."
- [ ] Maintenance mode shows the blue banner to every player and disables Challenge and Accept; the running-matches count drops as matches end.

## Release build
- [ ] `dotnet publish src/CromoBound.Server -c Release` has 0 warnings, and the published app passes "Signing in" and "Lobby" above.
```

- [ ] **Step 4: Run the tests and the release publish**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests).

Then run: `dotnet publish src/CromoBound.Server/CromoBound.Server.csproj -c Release -o <your scratch folder>/publish`
Expected: 0 warnings and 0 errors.
- The release build trims the WebAssembly app.
- If the publish reports any warning (for example a trim warning about the reflection-based JSON), stop and report it with the full text. Don't silence it.
- Delete the publish folder afterwards.

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Client tests/CromoBound.Client.Tests docs/client-manual-checks.md
git commit -m "feat(client): add the admin pages and the manual checks"
```

---

## Done criteria

- **Build and tests:** `dotnet build CromoBound.slnx --no-incremental` reports 0 warnings and 0 errors, and `dotnet test CromoBound.slnx` passes: the Models, Engine and Server suites, plus the new Client suite.
- **Release publish:** `dotnet publish src/CromoBound.Server -c Release` reports 0 warnings.
- **Signed out:** the app shows nothing but the login page (Plan I's tests still pass).
- **Lobby:** players with presence; challenge, accept, decline and cancel; a refused deck's problems; the maintenance and reconnecting banners; a short notification for every challenge received, declined, cancelled or withdrawn.
- **Decks:** pasted or uploaded decks kept per user in the browser, with rename and delete.
- **Match page:** opponent, format, game, stage and score; concede with confirmation; the result dialog; a wrong or finished match id goes back to the lobby.
- **Admin pages:** users with create, set password, role and disable; maintenance with the live running-matches count. A player opening them sees "You can't do that."
- **Session:** a 401 anywhere goes to `/login` once, and the session is renewed every 30 minutes while the app is open.
- **Look:** the pages use the approved look: Crimson night, rounded, Fredoka and Nunito.
- **Manual checks:** `docs/client-manual-checks.md` lists the checks to run against a real server before a deploy.
