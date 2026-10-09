# Client Plan I: Server Side of Phase 4a

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. First of two plans for Phase 4a (I: server side; J: the client app, written after the screens are mocked).

**Goal:** The server is ready for the Blazor app. The hub and HTTP records live in a shared contracts project, and the wire JSON reads back exactly. The app's files are served only to signed-in players, and the hub answers `GetLobby` and pushes presence and maintenance notices.

**Architecture:**
- **`CromoBound.Contracts`:** a new class library holding the hub records, `IGameClient`, the HTTP records and `WireJson`. The server and, in Plan J, the client both use it.
- **Wire JSON:** `WireJson` is the engine's JSON without indentation and with empty lists written as `[]`. The engine's events also read their `Sequence` back.
- **`CromoBound.Client`:** a minimal Blazor WebAssembly project with one placeholder component. The server references it and maps its files with `MapStaticAssets`, so they fall under the player-role fallback policy.
- **App routes:** a small middleware rewrites the app's routes to the static `/index.html`.
- **Presence:**
  - `LiveConnections` counts each user's connections.
  - A `Presence` service announces `PlayerChanged`/`PlayerLeft` to every other connected player.
  - `GetLobby` returns players, open challenges, the running match, the one-time abandoned notice and maintenance.

**Tech Stack:**
- .NET 10 (ASP.NET Core 10.0.12, SignalR).
- Microsoft.AspNetCore.Components.WebAssembly 10.0.12 (client) and Microsoft.AspNetCore.Components.WebAssembly.Server 10.0.12 (server).
- EF Core SQLite 10.0.12.
- Tests: xUnit 2.9.3, Mvc.Testing 10.0.12 and the SignalR client 10.0.12.

**Spec:** `docs/client.md` (sections 3, 4, 6.1, 6.2, 10, 12 server bullets, 13 row I). Builds on `docs/server.md` and `docs/server-plan-h.md`.

## Global Constraints

- **Language and placement:** `net10.0` with nullable enabled (from `Directory.Build.props`). Server types are `internal`, except `Program`. Everything in `CromoBound.Contracts` is public: the client uses it, and SignalR builds the typed proxy for `IGameClient` at run time.
- **Default deny (spec §4):**
  - Every endpoint needs a signed-in user with the player role, except `GET /login` and `POST /login`. That includes the app's static files, its `index.html` and the hub.
  - Admin endpoints need the admin policy.
  - `DefaultDenyTests` enforces both.
- **Role identifiers:** no response body, header, hub message, log line or error ever contains a role identifier or role name.
- **Privacy:** nothing reachable while signed out says what the site is. The app's `index.html` title is neutral ("Play").
- **Views:** a player is only ever sent their own seat's `PlayerView`. Presence notices carry names and two flags only.
- **Hub errors:** expected failures come back as replies; anything unexpected is SignalR's generic error; a bad call never closes the connection.
- **Owner rules:**
  - 0 build warnings and 0 errors at all times.
  - Conventional, title-only commit messages: no body, no co-author trailer, no mention of Claude/AI.
  - No em dashes or en dashes in code, comments or strings.
  - LF line endings; UTF-8 without BOM.
- **Packages:** the engine, models, data and contracts projects use no NuGet packages. The client adds only `Microsoft.AspNetCore.Components.WebAssembly`; the server adds only `Microsoft.AspNetCore.Components.WebAssembly.Server`.
- **dotnet:** run it with `export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && ` in Git Bash. The default dotnet on PATH is SDK 9. The build reports in Italian: `Avvisi` = warnings, `Errori` = errors, `Superato` = passed.

## Deliberate deviations from the spec (reviewers: these are intended)

1. **App routes are served by rewriting the request path to `/index.html`.** The path goes to the `index.html` static-asset endpoint, rather than through `MapFallbackToFile`.
   - A spike showed that `MapFallbackToFile` can't find the client's `index.html` in the in-memory test host. The static-asset endpoint works both there and in a published build.
   - The routes are an explicit list in `ClientApp`, which Plan J extends with each page it adds. Any other path stays a 404 instead of becoming the app.
2. **The app's `index.html` loads the non-fingerprinted `_framework/blazor.webassembly.js`, with no build-time placeholders.** The .NET 10 template's `#[.{fingerprint}]` placeholder isn't filled in when the server hosts the app's files; the spike saw it served raw. The non-fingerprinted framework files are mapped too, and a spike boot in headless Edge rendered the app.
3. **`GameEvent.Sequence` gets `[JsonInclude]` (an engine change).** Its setter is `internal`, so System.Text.Json never read it back, and every event in a client's view arrived with sequence 0. This changes nothing that is written, only what is read.
4. **Saved match records are written the new way too.** A record written by Plan H with an empty log (a match saved before any action) read back with a null log, and replaying it crashed, so a restart abandoned such a match. New records write `"log":[]`. Records already saved that way can't be repaired, and none are deployed. Spec §10's "records saved the old way still load" is corrected in Task 1 to say this.
5. **Presence is announced from the places that know a change happened**, by one `Presence.AnnounceAsync(userId)` that reads the account itself and sends `PlayerChanged` or `PlayerLeft`. Those places are: the hub (first and last connection, and a finished match after `Submit`), the lobby (match start) and the admin endpoints (create, disable, enable). Reading the account means a disabled user who disconnects is announced as left again, never as offline. That keeps them out of the other players' lists.
6. **The client project in this plan is only a shell:** a placeholder component that says the lobby is coming. Every real screen waits for Plan J and its approved mocks.

## Review Focus

1. **A signed-out visitor asking for any app file** (the framework script, the WebAssembly runtime, `index.html`, an app route) gets nothing: a bare 401 for files, and a redirect to `/login` for pages. Pinned in Task 2 (`Signed_out_the_app_pages_redirect_to_login_and_its_files_are_a_bare_401`, plus `DefaultDenyTests` over every static-asset endpoint).
2. **A path that isn't an app route** (`/nothing`, `/api/nothing`, `/match/not-a-guid`, `/decks/extra`) is a 404 even when signed in, never the app. Pinned in Task 2 (`Paths_that_arent_app_routes_are_not_found`).
3. **A connected player who is disabled** disappears from the others' lobbies and never comes back as offline when their connection drops. Pinned in Task 3 (`Disabling_a_connected_player_removes_them_and_their_disconnect_doesnt_bring_them_back`).
4. **A match saved before its first action** survives a restart. Pinned in Task 1 (`A_match_restarted_before_its_first_action_resumes`).
5. **Several tabs of one player:** presence turns online on the first tab and offline only when the last one closes. Pinned in Task 3 (`Presence_changes_only_with_the_first_and_the_last_tab`).

---

## File Structure

```
src/CromoBound.Contracts/
  CromoBound.Contracts.csproj        references Engine, Data, Models; no packages
  HubContracts.cs                    (moved from Server/Hubs/GameContracts.cs) replies, notices, lobby records
  IGameClient.cs                     (moved from Server/Hubs)
  AccountContracts.cs                (moved from Server/Accounts/Contracts.cs) LoginRequest, ErrorResponse, MeResponse
  AdminContracts.cs                  (moved from Server/Accounts) users and maintenance records
  WireJson.cs                        (replaces Server/ServerJson.cs) the wire JSON settings
src/CromoBound.Client/
  CromoBound.Client.csproj           Blazor WebAssembly; one package
  Program.cs, App.razor              a placeholder shell
  wwwroot/index.html                 the app's page
src/CromoBound.Engine/Events/GameEvents.cs   (modify: [JsonInclude] on Sequence)
src/CromoBound.Server/
  CromoBound.Server.csproj           (modify: Contracts, Client, WebAssembly.Server)
  ClientApp.cs                       the app's routes, rewritten to /index.html
  Program.cs                         (modify: rewrite, MapStaticAssets)
  Accounts/LoginEndpoints.cs, Accounts/Pages.cs   (modify: the placeholder home page goes)
  Accounts/LiveConnections.cs        (modify: counts per user)
  Accounts/AdminEndpoints.cs         (modify: announce presence)
  Hubs/GameHub.cs                    (modify: GetLobby, presence)
  Hubs/Presence.cs                   presence of a user, and its announcement
  Matches/Lobby.cs, Matches/MatchRegistry.cs, Matches/MaintenanceEndpoints.cs, Matches/MatchesSetup.cs   (modify)
tests/CromoBound.Engine.Tests/MatchJsonTests.cs   (modify: events keep their sequence)
tests/CromoBound.Server.Tests/
  WireJsonTests.cs, AppTests.cs, LobbyTests.cs, PresenceTests.cs   (create)
  GameClient.cs, RestartTests.cs, SignInTests.cs and the files that use moved records   (modify)
CromoBound.slnx, docs/client.md     (modify)
```

---

### Task 1: The contracts project and the wire JSON

**Files:**
- Create: `src/CromoBound.Contracts/CromoBound.Contracts.csproj`, `src/CromoBound.Contracts/WireJson.cs`
- Move (with `git mv`):
  - `src/CromoBound.Server/Hubs/GameContracts.cs` to `src/CromoBound.Contracts/HubContracts.cs`
  - `src/CromoBound.Server/Hubs/IGameClient.cs` to `src/CromoBound.Contracts/IGameClient.cs`
  - `src/CromoBound.Server/Accounts/Contracts.cs` to `src/CromoBound.Contracts/AccountContracts.cs`
  - `src/CromoBound.Server/Accounts/AdminContracts.cs` to `src/CromoBound.Contracts/AdminContracts.cs`
- Delete: `src/CromoBound.Server/ServerJson.cs`
- Modify: `src/CromoBound.Engine/Events/GameEvents.cs`, `src/CromoBound.Server/CromoBound.Server.csproj`, `CromoBound.slnx`, `docs/client.md`
- Modify: every server and test file that uses a moved type or `ServerJson` (add `using CromoBound.Contracts;`, and replace `ServerJson.Options` with `WireJson.Options`)
- Test: `tests/CromoBound.Engine.Tests/MatchJsonTests.cs`, `tests/CromoBound.Server.Tests/WireJsonTests.cs`, `tests/CromoBound.Server.Tests/RestartTests.cs`

**Interfaces:**
- Consumes:
  - Plan H's records (`HubReply`, `ChallengeEnd`, `ChallengeNotice`, `ChallengeClosedNotice`, `MatchStartedNotice`, `MatchViewNotice`, `MatchEndReason`, `MatchEndedNotice`, `MatchReply`, `SubmitReply`) and `IGameClient`.
  - Plan G's `LoginRequest`, `ErrorResponse`, `MeResponse`, `UserSummary`, `CreateUserRequest`, `PasswordRequest`, `RoleRequest` and `DisabledRequest`.
  - Plan H's `MaintenanceRequest` and `MaintenanceStatus`.
- Produces:
  - All of those types, unchanged, in namespace `CromoBound.Contracts` (assembly `CromoBound.Contracts`).
  - `WireJson.Options` (public), which replaces `ServerJson.Options` everywhere.
  - `GameEvent.Sequence` reads back from JSON.

- [ ] **Step 1: Create the contracts project and move the records**

Create `src/CromoBound.Contracts/CromoBound.Contracts.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <ItemGroup>
    <ProjectReference Include="..\CromoBound.Engine\CromoBound.Engine.csproj" />
    <ProjectReference Include="..\CromoBound.Data\CromoBound.Data.csproj" />
    <ProjectReference Include="..\CromoBound.Models\CromoBound.Models.csproj" />
  </ItemGroup>

</Project>
```

Add it to `CromoBound.slnx` in the `/src/` folder, keeping the alphabetical order:

```xml
    <Project Path="src/CromoBound.Contracts/CromoBound.Contracts.csproj" />
```

In `src/CromoBound.Server/CromoBound.Server.csproj`, add to the `ProjectReference` group:

```xml
    <ProjectReference Include="..\CromoBound.Contracts\CromoBound.Contracts.csproj" />
```

Move the four files with `git mv`, as listed under **Files**. In each moved file:
- Change the namespace line to `namespace CromoBound.Contracts;`.
- Leave every type, member and doc comment as it is.

The moved files keep their own `using` lines for engine and data types.

Delete `src/CromoBound.Server/ServerJson.cs`.

- [ ] **Step 2: Write the failing tests**

Add to `tests/CromoBound.Engine.Tests/MatchJsonTests.cs`, inside the class. Add `using CromoBound.Engine.Events;` at the top if it isn't there yet:

```csharp
    [Fact]
    public void Events_keep_their_sequence_through_json()
    {
        var match = Match.Create(TestDecks.Setup(MatchFormat.Bo1), EngineTestDb.Create()).Match!;
        var events = match.Events;

        var back = CromoJson.Deserialize<List<GameEvent>>(CromoJson.Serialize(events));

        Assert.True(events.Count > 2);
        Assert.Equal(events.Select(e => e.Sequence), back.Select(e => e.Sequence));
        Assert.All(back, e => Assert.True(e.Sequence > 0));
    }
```

Create `tests/CromoBound.Server.Tests/WireJsonTests.cs`:

```csharp
using System.Text.Json;
using CromoBound.Contracts;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Engine.Tests;
using CromoBound.Engine.Views;

namespace CromoBound.Server.Tests;

/// <summary>What travels on the hub and into saved records reads back exactly: empty lists stay empty lists, events keep their
/// numbers.</summary>
public class WireJsonTests
{
    [Fact]
    public void Every_view_of_a_scripted_game_reads_back_exactly()
    {
        var match = Match.Create(TestDecks.Setup(MatchFormat.Bo1, 5), ServerFactory.TestCards).Match!;
        var bots = new[] { new Bot(), new Bot() };

        for (var i = 0; i < 400 && match.Stage != MatchStage.Over; i++)
        {
            foreach (var seat in new[] { new PlayerId(0), new PlayerId(1) })
            {
                var json = JsonSerializer.Serialize(match.ViewFor(seat), WireJson.Options);
                Assert.Equal(json, JsonSerializer.Serialize(JsonSerializer.Deserialize<PlayerView>(json, WireJson.Options), WireJson.Options));
            }
            var player = match.Pending!.Players[0];
            Assert.True(match.Submit(player, bots[player.Index].Choose(match)).Accepted);
        }

        Assert.Equal(MatchStage.Over, match.Stage);
    }

    [Fact]
    public void Empty_lists_are_written_as_empty_lists()
    {
        var match = Match.Create(TestDecks.Setup(MatchFormat.Bo1), ServerFactory.TestCards).Match!;

        var json = JsonSerializer.Serialize(match.ToRecord(), WireJson.Options);

        Assert.Contains("\"log\":[]", json);
    }

    [Fact]
    public void The_wire_is_not_indented()
    {
        var json = JsonSerializer.Serialize(new HubReply(Guid.Empty, "x"), WireJson.Options);

        Assert.DoesNotContain('\n', json);
    }
}
```

Add to `tests/CromoBound.Server.Tests/RestartTests.cs`, after the first test:

```csharp
    [Fact]
    public async Task A_match_restarted_before_its_first_action_resumes()
    {
        var path = ServerFactory.NewDatabasePath();
        try
        {
            var matchId = await FirstServerAsync(path, (_, _) => Task.CompletedTask);

            using var second = new ServerFactory(databasePath: path);
            await using var again = await TwoPlayers.ReconnectAsync(second, matchId);

            Assert.Equal(matchId, (await again.First.GetMatchAsync()).MatchId);
            await again.PlayAsync();
            Assert.Equal(MatchEndReason.Finished, (await again.First.WaitForAsync<MatchEndedNotice>()).Reason);
        }
        finally
        {
            ServerFactory.DeleteDatabase(path);
        }
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile. `WireJson` doesn't exist, and every file that uses a moved type no longer finds it. That is expected; Step 4 fixes the code.

- [ ] **Step 4: Write the implementation**

Create `src/CromoBound.Contracts/WireJson.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CromoBound.Models.Json;

namespace CromoBound.Contracts;

/// <summary>What the hub speaks and how saved match records are written: the engine's JSON settings (<see cref="CromoJson.Options"/>),
/// same names, polymorphic type names and enums as strings, but unindented, and with empty lists written as <c>[]</c> instead of left
/// out, so whatever the client or a restart reads back is exactly what was written.</summary>
public static class WireJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(CromoJson.Options)
        {
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.MakeReadOnly();
        return options;
    }
}
```

`CromoJson`'s resolver carries one modifier, the one that leaves empty lists out of files. A plain `DefaultJsonTypeInfoResolver` drops it, and changes nothing else.

In `src/CromoBound.Engine/Events/GameEvents.cs`, mark the sequence so it reads back. The file already has `using System.Text.Json.Serialization;`:

```csharp
    /// <summary>The event's place in the match, from 1. Its setter is the engine's, so it is included explicitly to read back from JSON.</summary>
    [JsonInclude]
    public int Sequence { get; internal set; }
```

Then fix the compile errors from the move:
- Replace every `ServerJson.Options` with `WireJson.Options`. They are in `Matches/MatchStore.cs`, `Matches/MatchesSetup.cs`, `tests/CromoBound.Server.Tests/GameClient.cs` and `tests/CromoBound.Server.Tests/TwoPlayers.cs`.
- Add `using CromoBound.Contracts;` to every file that uses a moved type or `WireJson`. These are:
  - server: `Accounts/AccountsSetup.cs`, `Accounts/AdminEndpoints.cs`, `Accounts/LoginEndpoints.cs`, `Hubs/GameHub.cs`, `Hubs/SessionFilter.cs`, `Matches/Lobby.cs`, `Matches/MaintenanceEndpoints.cs`, `Matches/MatchHost.cs`, `Matches/MatchRegistry.cs`, `Matches/MatchStartup.cs`, `Matches/MatchStore.cs`, `Matches/MatchesSetup.cs`;
  - tests: `AdminTests.cs`, `ChallengeTests.cs`, `ErrorTests.cs`, `GameClient.cs`, `HubTests.cs`, `LoginProtectionTests.cs`, `MaintenanceTests.cs`, `MatchPlayTests.cs`, `MatchStartTests.cs`, `ProxyTests.cs`, `RestartTests.cs`, `ServerFactory.cs`, `SignInTests.cs`, `StartupTests.cs`, `TwoPlayers.cs`.

  Add it only where the compiler asks for it, and remove a `using CromoBound.Server.Hubs;` or `using CromoBound.Server.Accounts;` that becomes unused. A file still using `GameHub`, `Lobby` or another server type keeps its server usings.
- `IGameClient` is used as `Hub<IGameClient>` and `IHubContext<GameHub, IGameClient>` in the server; those files get the `using`.

In `docs/client.md`, replace section 10's last bullet with:

```markdown
- Saved records are written the same way, so a match saved before its first action keeps its empty log. A record written before this change with an empty log can't be replayed and is abandoned at startup; none were deployed.
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including the four new ones).

- [ ] **Step 6: Commit**

```bash
git add -A src tests CromoBound.slnx docs/client.md
git commit -m "refactor(server): move the hub and http records into a contracts project with exact wire json"
```

---

### Task 2: Serving the app behind sign-in

**Files:**
- Create: `src/CromoBound.Client/CromoBound.Client.csproj`, `src/CromoBound.Client/Program.cs`, `src/CromoBound.Client/App.razor`, `src/CromoBound.Client/wwwroot/index.html`
- Create: `src/CromoBound.Server/ClientApp.cs`
- Modify: `src/CromoBound.Server/CromoBound.Server.csproj`, `src/CromoBound.Server/Program.cs`, `src/CromoBound.Server/Accounts/LoginEndpoints.cs`, `src/CromoBound.Server/Accounts/Pages.cs`, `CromoBound.slnx`
- Test: `tests/CromoBound.Server.Tests/AppTests.cs` (create), `tests/CromoBound.Server.Tests/SignInTests.cs` (modify)

**Interfaces:**
- Consumes: Plan G's `Sessions.ChallengeAsync`, which redirects signed-out page requests to `/login` and gives everything else a bare 401; the fallback policy (player role).
- Produces:
  - `ClientApp.Page` (`"/index.html"`) and `ClientApp.IsAppRoute(PathString) : bool`.
  - `ClientApp.RewriteAsync(HttpContext, RequestDelegate)`.
  - The app's routes are `/`, `/decks`, `/admin/users`, `/admin/maintenance` and `/match/{guid}`. Plan J adds a route to `ClientApp` for each new page.

- [ ] **Step 1: Create the client shell**

Create `src/CromoBound.Client/CromoBound.Client.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly" Version="10.0.12" />
  </ItemGroup>

</Project>
```

Create `src/CromoBound.Client/Program.cs`:

```csharp
using CromoBound.Client;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
await builder.Build().RunAsync();
```

Create `src/CromoBound.Client/App.razor`:

```razor
@* A placeholder until Plan J builds the screens from the approved mocks. *@
<main>
    <p>The lobby is on its way.</p>
</main>
```

Create `src/CromoBound.Client/wwwroot/index.html`:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <meta name="robots" content="noindex, nofollow" />
    <title>Play</title>
    <base href="/" />
    <style>
        #blazor-error-ui { display: none; }
    </style>
</head>
<body>
    <div id="app"></div>
    <div id="blazor-error-ui">Something went wrong. <a href="" class="reload">Reload the page.</a></div>
    <script src="_framework/blazor.webassembly.js"></script>
</body>
</html>
```

Blazor shows `#blazor-error-ui` itself on an unhandled error, and its text never carries details.

Add the client to `CromoBound.slnx` in the `/src/` folder, keeping the alphabetical order:

```xml
    <Project Path="src/CromoBound.Client/CromoBound.Client.csproj" />
```

In `src/CromoBound.Server/CromoBound.Server.csproj`:
- Add to the `ProjectReference` group:

```xml
    <ProjectReference Include="..\CromoBound.Client\CromoBound.Client.csproj" />
```

- Add to the package group:

```xml
    <PackageReference Include="Microsoft.AspNetCore.Components.WebAssembly.Server" Version="10.0.12" />
```

- [ ] **Step 2: Write the failing tests**

Create `tests/CromoBound.Server.Tests/AppTests.cs`:

```csharp
using System.Net;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>The Blazor app (spec §4): every route and file of it needs a signed-in player, and nothing else becomes the app.</summary>
public class AppTests
{
    private const string AppScript = "_framework/blazor.webassembly.js";

    private static async Task<HttpClient> PlayerAsync(ServerFactory factory)
    {
        await factory.AddUserAsync("player1");
        return await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/decks")]
    [InlineData("/admin/users")]
    [InlineData("/admin/maintenance")]
    [InlineData("/match/6f9619ff-8b86-d011-b42d-00cf4fc964ff")]
    [InlineData("/DECKS")]
    public async Task Every_app_route_serves_the_app_to_a_signed_in_player(string route)
    {
        using var factory = new ServerFactory();
        var player = await PlayerAsync(factory);

        var response = await player.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(AppScript, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_app_script_is_served_to_a_signed_in_player()
    {
        using var factory = new ServerFactory();
        var player = await PlayerAsync(factory);

        var response = await player.GetAsync("/" + AppScript);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Signed_out_the_app_pages_redirect_to_login_and_its_files_are_a_bare_401()
    {
        using var factory = new ServerFactory();
        var client = factory.NewClient();

        foreach (var route in new[] { "/", "/decks", "/index.html" })
        {
            using var page = new HttpRequestMessage(HttpMethod.Get, route);
            page.Headers.Accept.ParseAdd("text/html");
            var redirected = await client.SendAsync(page);
            Assert.Equal(HttpStatusCode.Redirect, redirected.StatusCode);
            Assert.Equal("/login", redirected.Headers.Location?.OriginalString);
        }
        foreach (var file in new[] { "/index.html", "/" + AppScript, "/_framework/dotnet.js" })
        {
            var response = await client.GetAsync(file);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Paths_that_arent_app_routes_are_not_found()
    {
        using var factory = new ServerFactory();
        var player = await PlayerAsync(factory);

        foreach (var path in new[] { "/nothing", "/api/nothing", "/match/not-a-guid", "/decks/extra", "/admin" })
            Assert.Equal(HttpStatusCode.NotFound, (await player.GetAsync(path)).StatusCode);
    }

    [Fact]
    public void The_app_files_are_endpoints_so_default_deny_covers_them()
    {
        using var factory = new ServerFactory();

        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText?.TrimStart('/')).ToList();

        Assert.Contains(AppScript, routes);
        Assert.Contains("index.html", routes);
    }
}
```

In `tests/CromoBound.Server.Tests/SignInTests.cs`, in `A_form_sign_in_goes_home_and_a_failed_one_shows_the_generic_message`, replace:

```csharp
        Assert.Contains($"Signed in as {ServerFactory.AdminName}", await home.Content.ReadAsStringAsync());
```

with:

```csharp
        Assert.Contains("_framework/blazor.webassembly.js", await home.Content.ReadAsStringAsync());
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`, then `dotnet test CromoBound.slnx --filter "FullyQualifiedName~AppTests|FullyQualifiedName~SignInTests"`.
Expected:
- The build passes, and the client project builds with 0 warnings.
- The route tests fail: `/decks` and the other app routes are 404, and `/` still serves the old placeholder home page.
- The app-file tests fail: the static files aren't mapped yet.

- [ ] **Step 4: Write the implementation**

Create `src/CromoBound.Server/ClientApp.cs`:

```csharp
namespace CromoBound.Server;

/// <summary>The Blazor app's routes (spec §4). A request for one is served the app's <c>index.html</c>, by rewriting the path before
/// routing to the static-asset endpoint that serves it. That endpoint, like every file of the app, is behind the player policy, and a
/// path that isn't listed here stays a 404. Each page Plan J adds is added here.</summary>
internal static class ClientApp
{
    public const string Page = "/index.html";

    private static readonly string[] Routes = ["/", "/decks", "/admin/users", "/admin/maintenance"];

    private const string MatchPrefix = "/match/";

    public static bool IsAppRoute(PathString path)
    {
        var value = path.Value ?? "";
        if (Routes.Any(route => string.Equals(route, value, StringComparison.OrdinalIgnoreCase))) return true;
        return value.StartsWith(MatchPrefix, StringComparison.OrdinalIgnoreCase) && Guid.TryParseExact(value[MatchPrefix.Length..], "D", out _);
    }

    public static Task RewriteAsync(HttpContext context, RequestDelegate next)
    {
        if (HttpMethods.IsGet(context.Request.Method) && IsAppRoute(context.Request.Path)) context.Request.Path = Page;
        return next(context);
    }
}
```

In `src/CromoBound.Server/Program.cs`:
- Before `app.UseRouting();`, add:

```csharp
// The app's routes are served its index.html (behind the player policy, like all of its files).
app.Use(ClientApp.RewriteAsync);
```

- After the `app.MapHub<GameHub>(...)` line, add:

```csharp
// The Blazor app's files, as endpoints, so the fallback policy keeps them behind sign-in.
app.MapStaticAssets();
```

In `src/CromoBound.Server/Accounts/LoginEndpoints.cs`:
- Delete the `app.MapGet("/", ...)` mapping (with its `.RequireAuthorization(Policies.Seat)`). The app now serves `/`.
- Update the class summary to "Sign-in, sign-out and who am I (spec §4.4, §5.2); the home page is the Blazor app."
- Remove any `using` that becomes unused.

In `src/CromoBound.Server/Accounts/Pages.cs`:
- Delete the `Home` method.
- Change the summary to "The server-made login page, which names nothing about the site (spec §1). Everything else is the Blazor app."
- Remove `using System.Text.Encodings.Web;` if nothing else uses it.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests). `DefaultDenyTests` now walks every static-asset endpoint too, and passes, because the fallback policy covers them.

- [ ] **Step 6: Check a published build serves the app**

Docker isn't available on the development machine, so check the publish output directly:

```bash
export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && dotnet publish src/CromoBound.Server/CromoBound.Server.csproj -c Release -o "$TEMP/cromobound-publish" -p:UseAppHost=false
ls "$TEMP/cromobound-publish/wwwroot" "$TEMP/cromobound-publish/wwwroot/_framework" | grep -E "index.html|blazor.webassembly.js$"
grep -c '"Route":"index.html"' "$TEMP/cromobound-publish/CromoBound.Server.staticwebassets.endpoints.json"
rm -rf "$TEMP/cromobound-publish"
```

Expected:
- The publish reports 0 warnings and 0 errors.
- `index.html` and `blazor.webassembly.js` are listed.
- The route count is at least 1.

The Dockerfile needs no change: its publish step builds the client too.

- [ ] **Step 7: Commit**

```bash
git add src/CromoBound.Client src/CromoBound.Server tests/CromoBound.Server.Tests CromoBound.slnx
git commit -m "feat(server): serve the blazor app to signed-in players only"
```

---
### Task 3: The lobby and presence

**Files:**
- Create: `src/CromoBound.Server/Hubs/Presence.cs`
- Modify: `src/CromoBound.Contracts/HubContracts.cs`, `src/CromoBound.Contracts/IGameClient.cs`
- Modify: `src/CromoBound.Server/Accounts/LiveConnections.cs`, `src/CromoBound.Server/Accounts/AdminEndpoints.cs`, `src/CromoBound.Server/Hubs/GameHub.cs`
- Modify: `src/CromoBound.Server/Matches/Lobby.cs`, `src/CromoBound.Server/Matches/MatchRegistry.cs`, `src/CromoBound.Server/Matches/MaintenanceEndpoints.cs`, `src/CromoBound.Server/Matches/MatchesSetup.cs`
- Modify: `tests/CromoBound.Server.Tests/GameClient.cs`, `tests/CromoBound.Server.Tests/RestartTests.cs`
- Create: `tests/CromoBound.Server.Tests/LobbyTests.cs`, `tests/CromoBound.Server.Tests/PresenceTests.cs`

**Interfaces:**
- Consumes:
  - From Task 1: the contracts in `CromoBound.Contracts`.
  - From Plan H:
    - `LiveConnections.Opened`, `Closed` and `EndAll`;
    - `MatchRegistry` (`IsPlaying`, `Find`, `SubmitAsync`, `NoteAbandoned`);
    - `Lobby.AcceptAsync`;
    - `MatchHost.Seats`;
    - `Maintenance.On`;
    - `GameHub.UserGroup`;
    - in the tests, `TwoPlayers` and `GameClient`.
- Produces:
  - Contracts:
    - `PlayerPresence(string UserName, bool Online, bool InMatch)`;
    - `ChallengeInfo(Guid ChallengeId, string From, string To, MatchFormat Format)`;
    - `LobbyReply(IReadOnlyList<PlayerPresence> Players, IReadOnlyList<ChallengeInfo> Challenges, Guid? MatchId, MatchEndedNotice? Ended, bool Maintenance)`;
    - `PlayerLeftNotice(string UserName)` and `MaintenanceNotice(bool On)`.
  - `MatchReply` loses `Ended`: it is now `MatchReply(Guid? MatchId, PlayerView? View)`.
  - `IGameClient.PlayerChanged`, `PlayerLeft` and `MaintenanceChanged`.
  - The hub method `GetLobby() : LobbyReply`.
  - `LiveConnections`:
    - `Opened(...)` now returns `bool` (the user's first connection);
    - `Closed(string connectionId, out int userId)` returns `bool` (the user's last connection);
    - new `IsOnline(int)` and `ConnectionsOf(int)`.
  - `Presence.Of(int userId, string userName)` and `Presence.AnnounceAsync(int userId)`.
  - `MatchRegistry.MatchOf(int)` and `MatchRegistry.TakeAbandoned(int)`.
  - `Lobby.ChallengesOfAsync(int)`.
  - In the tests: `GameClient.GetLobbyAsync()` and `GameClient.WaitForAsync<T>(Func<T, bool> match, int after)`.

- [ ] **Step 1: Write the failing tests**

In `tests/CromoBound.Server.Tests/GameClient.cs`:
- Add these lines to the constructor, after the `MatchEndedNotice` line:

```csharp
        Record<PlayerPresence>(nameof(IGameClient.PlayerChanged));
        Record<PlayerLeftNotice>(nameof(IGameClient.PlayerLeft));
        Record<MaintenanceNotice>(nameof(IGameClient.MaintenanceChanged));
```

- Add after `GetMatchAsync`:

```csharp
    public Task<LobbyReply> GetLobbyAsync() => Connection.InvokeAsync<LobbyReply>("GetLobby");
```

- Replace `WaitForAsync` with these two methods:

```csharp
    /// <summary>The first notice of this type (matching <paramref name="match"/>, if given), waiting up to ten seconds for it.</summary>
    public Task<T> WaitForAsync<T>(Func<T, bool>? match = null) => WaitForAsync(match ?? (_ => true), after: 0);

    /// <summary>The first notice of this type matching <paramref name="match"/> among those after the first <paramref name="after"/>
    /// of its type, waiting up to ten seconds for it.</summary>
    public async Task<T> WaitForAsync<T>(Func<T, bool> match, int after)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (true)
        {
            lock (_received)
                foreach (var notice in _received.OfType<T>().Skip(after))
                    if (match(notice)) return notice;
            await _arrived.WaitAsync(timeout.Token);
        }
    }
```

Create `tests/CromoBound.Server.Tests/LobbyTests.cs`:

```csharp
using System.Net.Http.Json;
using CromoBound.Contracts;
using CromoBound.Engine.Matches;

namespace CromoBound.Server.Tests;

/// <summary>GetLobby (spec §6.1): one call that restores everything the lobby shows.</summary>
public class LobbyTests
{
    [Fact]
    public async Task The_lobby_lists_every_other_enabled_player_by_name_with_their_presence()
    {
        using var factory = new ServerFactory();
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await factory.AddUserAsync("dave");
        await factory.AddUserAsync("erin");
        await factory.WithStoreAsync(async users => await users.SetDisabledAsync((await users.FindAsync("erin"))!, true));

        var lobby = await carol.GetLobbyAsync();

        Assert.Equal(
            new[] { new PlayerPresence(ServerFactory.AdminName, false, false), new PlayerPresence("bob", true, false), new PlayerPresence("dave", false, false) },
            lobby.Players);
        Assert.Empty(lobby.Challenges);
        Assert.Null(lobby.MatchId);
        Assert.Null(lobby.Ended);
        Assert.False(lobby.Maintenance);
    }

    [Fact]
    public async Task Players_in_a_match_show_as_in_a_match_and_their_own_lobby_names_it()
    {
        using var factory = new ServerFactory();
        await using var players = await TwoPlayers.StartAsync(factory);
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");

        var seen = await carol.GetLobbyAsync();

        Assert.Contains(new PlayerPresence("alice", true, true), seen.Players);
        Assert.Contains(new PlayerPresence("bob", true, true), seen.Players);
        Assert.Null(seen.MatchId);
        Assert.Equal(players.MatchId, (await players.First.GetLobbyAsync()).MatchId);
        Assert.Equal(players.MatchId, (await players.Second.GetLobbyAsync()).MatchId);
    }

    [Fact]
    public async Task Your_open_challenges_come_back_after_a_reconnect()
    {
        using var factory = new ServerFactory();
        var alice = await GameClient.NewPlayerAsync(factory, "alice");
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        var id = (await alice.ChallengeAsync("bob", Decks.First, MatchFormat.Bo3)).Id!.Value;
        await alice.DisposeAsync();

        await using var again = await GameClient.ConnectAsync(factory, "alice");
        var expected = new ChallengeInfo(id, "alice", "bob", MatchFormat.Bo3);

        Assert.Equal(new[] { expected }, (await again.GetLobbyAsync()).Challenges);
        Assert.Equal(new[] { expected }, (await bob.GetLobbyAsync()).Challenges);
        Assert.Empty((await carol.GetLobbyAsync()).Challenges);
    }

    [Fact]
    public async Task Maintenance_shows_in_the_lobby_and_its_switch_is_announced_to_everyone()
    {
        using var factory = new ServerFactory();
        await using var alice = await GameClient.NewPlayerAsync(factory, "alice");
        var admin = await factory.SignInAdminAsync();

        await admin.PostAsJsonAsync("/api/admin/maintenance", new MaintenanceRequest(true));

        Assert.Equal(new MaintenanceNotice(true), await alice.WaitForAsync<MaintenanceNotice>());
        Assert.True((await alice.GetLobbyAsync()).Maintenance);

        await admin.PostAsJsonAsync("/api/admin/maintenance", new MaintenanceRequest(false));

        Assert.Equal(new MaintenanceNotice(false), await alice.WaitForAsync<MaintenanceNotice>(n => !n.On));
        Assert.False((await alice.GetLobbyAsync()).Maintenance);
    }
}
```

Create `tests/CromoBound.Server.Tests/PresenceTests.cs`:

```csharp
using System.Net.Http.Json;
using CromoBound.Contracts;
using CromoBound.Engine.Actions;

namespace CromoBound.Server.Tests;

/// <summary>Presence notices (spec §6.2): every other connected player hears when someone comes, goes, starts or ends a match, or is
/// created, disabled or enabled. No one hears about themselves.</summary>
public class PresenceTests
{
    private static IReadOnlyList<PlayerPresence> About(GameClient client, string userName) =>
        [.. client.All<PlayerPresence>().Where(p => p.UserName == userName)];

    [Fact]
    public async Task Presence_changes_only_with_the_first_and_the_last_tab()
    {
        using var factory = new ServerFactory();
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await factory.AddUserAsync("alice");

        var first = await GameClient.ConnectAsync(factory, "alice");
        await bob.WaitForAsync<PlayerPresence>(p => p is { UserName: "alice", Online: true });
        var second = await GameClient.ConnectAsync(factory, "alice");
        await first.DisposeAsync();
        await second.DisposeAsync();
        await bob.WaitForAsync<PlayerPresence>(p => p is { UserName: "alice", Online: false });

        Assert.Equal(new[] { new PlayerPresence("alice", true, false), new PlayerPresence("alice", false, false) }, About(bob, "alice"));
        Assert.Empty(About(first, "alice"));
        Assert.Empty(About(second, "alice"));
    }

    [Fact]
    public async Task Starting_and_ending_a_match_is_announced_to_the_other_players()
    {
        using var factory = new ServerFactory();
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        await using var players = await TwoPlayers.StartAsync(factory);

        await carol.WaitForAsync<PlayerPresence>(p => p is { UserName: "alice", InMatch: true });
        await carol.WaitForAsync<PlayerPresence>(p => p is { UserName: "bob", InMatch: true });
        var seen = carol.All<PlayerPresence>().Count;

        Assert.True((await players.First.SubmitAsync(players.MatchId, new Concede())).Accepted);

        foreach (var name in new[] { "alice", "bob" })
            Assert.Equal(new PlayerPresence(name, true, false), await carol.WaitForAsync<PlayerPresence>(p => p.UserName == name, after: seen));
    }

    [Fact]
    public async Task Admins_creating_disabling_and_enabling_a_user_is_announced()
    {
        using var factory = new ServerFactory();
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        var admin = await factory.SignInAdminAsync();

        var created = await admin.PostAsJsonAsync("/api/admin/users", new CreateUserRequest("erin", ServerFactory.PlayerPassword, false));
        var erin = (await created.Content.ReadFromJsonAsync<UserSummary>())!;

        Assert.Equal(new PlayerPresence("erin", false, false), await bob.WaitForAsync<PlayerPresence>(p => p.UserName == "erin"));
        await admin.PutAsJsonAsync($"/api/admin/users/{erin.Id}/disabled", new DisabledRequest(true));
        Assert.Equal(new PlayerLeftNotice("erin"), await bob.WaitForAsync<PlayerLeftNotice>());
        var seen = bob.All<PlayerPresence>().Count;
        await admin.PutAsJsonAsync($"/api/admin/users/{erin.Id}/disabled", new DisabledRequest(false));
        Assert.Equal(new PlayerPresence("erin", false, false), await bob.WaitForAsync<PlayerPresence>(p => p.UserName == "erin", after: seen));
    }

    [Fact]
    public async Task Disabling_a_connected_player_removes_them_and_their_disconnect_doesnt_bring_them_back()
    {
        using var factory = new ServerFactory();
        await using var bob = await GameClient.NewPlayerAsync(factory, "bob");
        await using var carol = await GameClient.NewPlayerAsync(factory, "carol");
        await bob.WaitForAsync<PlayerPresence>(p => p is { UserName: "carol", Online: true });
        var seen = bob.All<PlayerPresence>().Count;
        var admin = await factory.SignInAdminAsync();
        var carolId = (await admin.GetFromJsonAsync<List<UserSummary>>("/api/admin/users"))!.Single(u => u.UserName == "carol").Id;

        await admin.PutAsJsonAsync($"/api/admin/users/{carolId}/disabled", new DisabledRequest(true));

        await carol.Closed.WaitAsync(TimeSpan.FromSeconds(10));
        await bob.WaitForAsync<PlayerLeftNotice>(n => n.UserName == "carol");
        await bob.WaitForAsync<PlayerLeftNotice>(n => n.UserName == "carol", after: 1);
        Assert.DoesNotContain(bob.All<PlayerPresence>().Skip(seen), p => p.UserName == "carol");
        Assert.DoesNotContain((await bob.GetLobbyAsync()).Players, p => p.UserName == "carol");
    }
}
```

The last test waits for both `PlayerLeft` notices: one from the admin endpoint, one from carol's last connection closing. Only then does it check that no `PlayerChanged` about carol came after the disable.

In `tests/CromoBound.Server.Tests/RestartTests.cs`, the abandoned notice now comes from `GetLobby`:
- In `A_match_saved_by_another_engine_build_is_abandoned_and_its_players_are_told_once`:
  - `var told = await alice.GetMatchAsync();` becomes `var told = await alice.GetLobbyAsync();`.
  - `Assert.Equal(MatchReply.None, await alice.GetMatchAsync());` becomes `Assert.Null((await alice.GetLobbyAsync()).Ended);`.
  - `(await bob.GetMatchAsync()).Ended!.Reason` becomes `(await bob.GetLobbyAsync()).Ended!.Reason`.
- In `An_unreadable_match_record_is_abandoned_and_the_server_still_starts`, `(await alice.GetMatchAsync()).Ended!.MatchId` becomes `(await alice.GetLobbyAsync()).Ended!.MatchId`.
- In `A_match_whose_seat_user_no_longer_exists_is_abandoned_and_the_server_still_starts`:
  - `var told = await alice.GetMatchAsync();` becomes `var told = await alice.GetLobbyAsync();`.
  - `Assert.Equal(MatchReply.None, await alice.GetMatchAsync());` becomes `Assert.Null((await alice.GetLobbyAsync()).Ended);`.
- `A_finished_match_stays_finished_and_keeps_its_record` keeps `Assert.Equal(MatchReply.None, await alice.GetMatchAsync());` unchanged.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`PlayerPresence`, `LobbyReply`, `GetLobbyAsync` and the rest don't exist yet).

- [ ] **Step 3: Write the implementation**

In `src/CromoBound.Contracts/HubContracts.cs`, replace `MatchReply` with:

```csharp
/// <summary>GetMatch's answer: the player's running match and their view of it, or nothing.</summary>
public sealed record MatchReply(Guid? MatchId, PlayerView? View)
{
    public static MatchReply None { get; } = new(null, null);
}
```

Append to `src/CromoBound.Contracts/HubContracts.cs`:

```csharp

/// <summary>How another player stands: connected (at least one open tab) and in a running match.</summary>
public sealed record PlayerPresence(string UserName, bool Online, bool InMatch);

/// <summary>An open challenge the receiving player made or received.</summary>
public sealed record ChallengeInfo(Guid ChallengeId, string From, string To, MatchFormat Format);

/// <summary>GetLobby's answer (spec §6.1): every other enabled player by name, the caller's open challenges, their running match,
/// once the notice of a match of theirs abandoned when the server restarted, and whether maintenance is on.</summary>
public sealed record LobbyReply(
    IReadOnlyList<PlayerPresence> Players, IReadOnlyList<ChallengeInfo> Challenges, Guid? MatchId, MatchEndedNotice? Ended, bool Maintenance);

/// <summary>A player was disabled and leaves the lobby.</summary>
public sealed record PlayerLeftNotice(string UserName);

public sealed record MaintenanceNotice(bool On);
```

In `src/CromoBound.Contracts/IGameClient.cs`, add after `MatchEnded`:

```csharp

    /// <summary>Another player came, went, or started or ended a match, or an admin created or re-enabled them.</summary>
    Task PlayerChanged(PlayerPresence player);

    Task PlayerLeft(PlayerLeftNotice left);

    Task MaintenanceChanged(MaintenanceNotice maintenance);
```

Replace `src/CromoBound.Server/Accounts/LiveConnections.cs` with:

```csharp
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
```

The local copy `user` is there because a lambda can't capture an `out` parameter.

Create `src/CromoBound.Server/Hubs/Presence.cs`:

```csharp
using CromoBound.Contracts;
using CromoBound.Server.Accounts;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Hubs;

/// <summary>How players stand in the lobby (spec §6.2): online when they have an open connection, and in a match.</summary>
internal sealed class Presence(LiveConnections connections, MatchRegistry matches, IHubContext<GameHub, IGameClient> hub, IServiceScopeFactory scopes)
{
    public PlayerPresence Of(int userId, string userName) => new(userName, connections.IsOnline(userId), matches.IsPlaying(userId));

    /// <summary>Tells every other connected player how the user stands now: PlayerChanged while the account is enabled, PlayerLeft once
    /// it is disabled. The account is read here, so a disabled user's last disconnect announces them as left again, never as offline. The
    /// user's own connections aren't told.</summary>
    public async Task AnnounceAsync(int userId)
    {
        using var scope = scopes.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<CromoDbContext>().Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == userId);
        if (user is null) return;
        var others = hub.Clients.AllExcept(connections.ConnectionsOf(userId));
        if (user.Disabled) await others.PlayerLeft(new PlayerLeftNotice(user.UserName));
        else await others.PlayerChanged(Of(user.Id, user.UserName));
    }
}
```

In `src/CromoBound.Server/Matches/MatchRegistry.cs`:
- Replace `CurrentAsync` with:

```csharp
    /// <summary>The user's running match and their view of it, or nothing.</summary>
    public async Task<MatchReply> CurrentAsync(int userId) =>
        _byUser.TryGetValue(userId, out var host) && host.SeatOf(userId) is { } seat
            ? new MatchReply(host.Id, await host.ViewAsync(seat))
            : MatchReply.None;

    public Guid? MatchOf(int userId) => _byUser.TryGetValue(userId, out var host) ? host.Id : null;

    /// <summary>The notice of a match of the user's abandoned at startup, handed out once.</summary>
    public MatchEndedNotice? TakeAbandoned(int userId) => _abandoned.TryRemove(userId, out var ended) ? ended : null;
```

- Change `NoteAbandoned`'s summary to "Keeps the notice until the user next asks for their lobby (in memory: a later restart forgets it)."

In `src/CromoBound.Server/Matches/Lobby.cs`:
- Add `Presence presence` to the constructor: `internal sealed class Lobby(CardDatabase cards, IMatchStore store, MatchRegistry matches, Maintenance maintenance, Presence presence, IHubContext<GameHub, IGameClient> hub, IServiceScopeFactory scopes)`.
- In `AcceptAsync`, after `await host.StartAsync();`, add:

```csharp
            await presence.AnnounceAsync(challenge.From.UserId);
            await presence.AnnounceAsync(challenge.To.UserId);
```

- Add this method after `CancelAsync`:

```csharp
    /// <summary>The user's open challenges, made and received (spec §6.1).</summary>
    public async Task<IReadOnlyList<ChallengeInfo>> ChallengesOfAsync(int userId)
    {
        await _gate.WaitAsync();
        try
        {
            return [.. _open.Values.Where(c => c.From.UserId == userId || c.To.UserId == userId)
                .Select(c => new ChallengeInfo(c.Id, c.From.UserName, c.To.UserName, c.Format))];
        }
        finally
        {
            _gate.Release();
        }
    }
```

Replace `src/CromoBound.Server/Hubs/GameHub.cs` with:

```csharp
using System.Globalization;
using CromoBound.Contracts;
using CromoBound.Engine.Actions;
using CromoBound.Engine.Matches;
using CromoBound.Models.Cards;
using CromoBound.Server.Accounts;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CromoBound.Server.Hubs;

/// <summary>The one hub (spec §6), at /hub behind the player policy. Every connection joins its user's group, so all of a user's tabs
/// get their notices. Methods answer with replies; anything unexpected is SignalR's generic error. A user's first connection and last
/// disconnection, and the end of a match, are announced to the other players.</summary>
internal sealed class GameHub(Lobby lobby, MatchRegistry matches, LiveConnections connections, Presence presence, Maintenance maintenance,
    CromoDbContext db) : Hub<IGameClient>
{
    public static string UserGroup(int userId) => "user-" + userId.ToString(CultureInfo.InvariantCulture);

    private MatchSeat Me => new(Sessions.UserId(Context.User!)!.Value, Context.User!.Identity!.Name!);

    /// <summary>The connection is tracked before its session is checked again, so an account change made in between still closes it.
    /// Only a connection that passes the check is announced.</summary>
    public override async Task OnConnectedAsync()
    {
        var me = Me;
        var first = connections.Opened(Context.ConnectionId, me.UserId, Context.Abort);
        if (!await Sessions.IsCurrentAsync(Context.User!, db))
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(me.UserId));
        if (first) await presence.AnnounceAsync(me.UserId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (connections.Closed(Context.ConnectionId, out var userId)) await presence.AnnounceAsync(userId);
    }

    /// <summary>Everything the lobby shows, on connect and on reconnect (spec §6.1). The abandoned notice is handed out once.</summary>
    public async Task<LobbyReply> GetLobby()
    {
        var me = Me;
        var others = await db.Users.AsNoTracking().Where(u => !u.Disabled && u.Id != me.UserId)
            .OrderBy(u => u.NormalizedUserName).Select(u => new { u.Id, u.UserName }).ToListAsync();
        return new LobbyReply(
            [.. others.Select(u => presence.Of(u.Id, u.UserName))],
            await lobby.ChallengesOfAsync(me.UserId),
            matches.MatchOf(me.UserId),
            matches.TakeAbandoned(me.UserId),
            maintenance.On);
    }

    public Task<HubReply> Challenge(string? opponent, MatchFormat format, Deck? deck) => lobby.ChallengeAsync(Me, opponent, format, deck);

    public Task<HubReply> AcceptChallenge(Guid challengeId, Deck? deck) => lobby.AcceptAsync(Me, challengeId, deck);

    public Task<HubReply> DeclineChallenge(Guid challengeId) => lobby.DeclineAsync(Me, challengeId);

    public Task<HubReply> CancelChallenge(Guid challengeId) => lobby.CancelAsync(Me, challengeId);

    /// <summary>The caller's running match and view.</summary>
    public Task<MatchReply> GetMatch() => matches.CurrentAsync(Me.UserId);

    /// <summary>Any action, manual ones, undo and concede included, in the caller's own seat (spec §6.3). An action that ends the match
    /// announces both players as free again.</summary>
    public async Task<SubmitReply> Submit(Guid matchId, PlayerAction? action)
    {
        var host = matches.Find(matchId);
        var reply = await matches.SubmitAsync(Me.UserId, matchId, action);
        if (reply.Accepted && host is not null && matches.Find(matchId) is null)
            foreach (var seat in host.Seats) await presence.AnnounceAsync(seat.UserId);
        return reply;
    }
}
```

In `src/CromoBound.Server/Accounts/AdminEndpoints.cs`:
- Add `using CromoBound.Server.Hubs;`.
- Replace `CreateAsync` and `SetDisabledAsync` with:

```csharp
    private static async Task<IResult> CreateAsync(CreateUserRequest request, UserStore users, Presence presence)
    {
        var (user, error) = await users.CreateAsync(request.UserName ?? "", request.Password ?? "", request.IsAdmin);
        if (user is null) return Results.BadRequest(new ErrorResponse(error!));
        await presence.AnnounceAsync(user.Id);
        return Results.Created($"/api/admin/users/{user.Id}", Summary(user));
    }
```

```csharp
    /// <summary>The body must say which; a flag the user already has changes nothing (their sessions go on). The other players hear that
    /// the user left or came back.</summary>
    private static async Task<IResult> SetDisabledAsync(int id, DisabledRequest request, UserStore users, ClaimsPrincipal me, Presence presence)
    {
        if (request.Disabled is not { } disabled) return Results.BadRequest(new ErrorResponse(SayDisabled));
        if (await users.FindAsync(id) is not { } user) return Results.NotFound();
        if (user.Disabled == disabled) return Results.NoContent();
        if (disabled && await RefusalAsync(user, me, users) is { } problem) return Results.BadRequest(new ErrorResponse(problem));
        await users.SetDisabledAsync(user, disabled);
        await presence.AnnounceAsync(user.Id);
        return Results.NoContent();
    }
```

In `src/CromoBound.Server/Matches/MaintenanceEndpoints.cs`:
- Add `using CromoBound.Server.Hubs;` and `using Microsoft.AspNetCore.SignalR;`.
- Map the POST with `maintenance.MapPost("", SwitchAsync);`.
- Replace `Switch` with:

```csharp
    /// <summary>The body must say which. Every connected player hears the switch.</summary>
    private static async Task<IResult> SwitchAsync(MaintenanceRequest request, Maintenance state, MatchRegistry matches,
        IHubContext<GameHub, IGameClient> hub, ILogger<Maintenance> log)
    {
        if (request.On is not { } on) return Results.BadRequest(new ErrorResponse(SayOn));
        state.On = on;
        log.LogInformation("Maintenance is {State}.", on ? "on" : "off");
        await hub.Clients.All.MaintenanceChanged(new MaintenanceNotice(on));
        return Results.Ok(Status(state, matches));
    }
```

In `src/CromoBound.Server/Matches/MatchesSetup.cs`:
- Add `services.AddSingleton<Presence>();` after `services.AddSingleton<Maintenance>();`.
- Add "the presence announcements" to the summary's list.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, including `LobbyTests`, `PresenceTests`, the updated `RestartTests` and `DefaultDenyTests`).

Then run `dotnet test CromoBound.slnx --filter "FullyQualifiedName~PresenceTests|FullyQualifiedName~LobbyTests"` five times in a row. Expected: PASS every time. These tests depend on notice timing, so a flake here is a bug to fix, not to retry.

- [ ] **Step 5: Commit**

```bash
git add src tests
git commit -m "feat(server): add the lobby call and presence notices"
```

---

## Done criteria

- [ ] The hub and HTTP records live in `CromoBound.Contracts`. The wire JSON keeps empty lists and event numbers, so views and records read back exactly, and a match saved before its first action survives a restart (Task 1).
- [ ] The Blazor app's routes and files are served only to signed-in players. Signed out, pages redirect to `/login` and files are a bare 401. Other paths stay 404, and the placeholder home page is gone (Task 2).
- [ ] `GetLobby` restores the players with their presence, your open challenges (after a reconnect too), your match, the one-time abandoned notice and maintenance (Task 3).
- [ ] Presence and maintenance notices reach every other connected player, and a disabled player never comes back as offline (Task 3).
- [ ] `dotnet build CromoBound.slnx --no-incremental` reports 0 warnings and 0 errors, and `dotnet test CromoBound.slnx` passes.
