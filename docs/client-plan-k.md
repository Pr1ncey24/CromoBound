# Client Plan K: Card Images and the Card Catalog

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. First of two plans for Phase 4b-1 (K: images and catalog; L: the board).

**Goal:** Every card image is downloaded once by the importer and served by the server to signed-in players, and the client can load a compact card catalog.

**Architecture:**
- **The importer** gains an `images` command. A `CardImageDownloader` fetches each printing's PNG into one folder (`{printingId}.png`). It skips existing files, runs at most 4 downloads at once, and saves only PNG answers, through a temporary file.
- **The server** gains a `Cards` area:
  - `GET /api/cards` answers a `CardCatalog`, built once from the card database and written in the hub's wire JSON;
  - `GET /cards/img/{printingId}` answers the PNG from a new `CardImagesPath` folder, for printings the database knows.
  - Both routes are for signed-in players only.
- **The Docker image and the deploy guide** gain the image folder.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, xUnit 2.9.3.

**Spec:** `docs/client-4b.md` (sections 3, 9, 10 server and importer bullets, 11 row K).

## Global Constraints

- **Language:** `net10.0` with nullable enabled and implicit usings (from `Directory.Build.props`).
- **Privacy and security:**
  - Both new routes require the player policy (`Policies.Seat`). Signed out, they answer a bare 401 like the app's files.
  - Nothing outside the image folder can be asked for: only ids the card database knows are served.
  - Responses never name a role.
- **Packages:** none added anywhere.
- **Wire format:** the catalog is written with `WireJson.Options` (camelCase, enums as strings, empty lists as `[]`), the same settings the hub uses. The client reads it with the same options.
- **Owner rules:**
  - 0 build warnings and 0 errors at all times.
  - Conventional, title-only commit messages: no body, no co-author trailer, no mention of Claude/AI.
  - No em dashes or en dashes in code, comments, strings, markup or docs.
  - LF line endings; UTF-8 without BOM.
- **dotnet:** run it with `export PATH="/c/Program Files/dotnet:$PATH" DOTNET_ROOT="C:\\Program Files\\dotnet" && ` in Git Bash. The build reports in Italian: `Avvisi` = warnings, `Errori` = errors, `Superato` = passed.

## Deliberate deviations from the spec (reviewers: these are intended)

1. **Tokens are in the catalog's `Cards` list, not a separate `Tokens` list (spec §3.3).**
   - The card database already keeps tokens as cards with the `Token` supertype.
   - Each catalog card carries its supertype, so the client tells them apart.
   - Tokens have no printings, so they have no images.
2. **The catalog's printings also carry their orientation** (portrait or landscape), so the board never stretches a landscape image.
3. **The catalog is not cached by the browser** (spec §3.3 said `private, max-age=3600`). Every `/api` answer is `no-store` (`Privacy.HeadersAsync`), and the client loads the catalog only once per tab anyway, so the rule stays as it is.
4. **Only PNG answers are saved.** All 1451 printings link to PNG files, and the server serves `image/png`, so any other content type counts as a failure. That is how the spec's "the answer is an image" check is made concrete.

## Review Focus

1. **A path or id that tries to leave the image folder** (`..`, encoded slashes, a full path) gets a 404, never a file. Pinned in Task 2 (`Unknown_printings_traversal_and_missing_files_are_not_found`).
2. **A download that fails halfway** (an error page, a non-image, an empty body, a dropped connection) leaves no file behind, so the next run tries it again. Pinned in Task 1 (`Failed_downloads_leave_no_file_and_are_reported`).
3. **Running the command twice** fetches nothing the second time. Pinned in Task 1 (`Files_already_there_are_skipped`).
4. **A printing in the catalog has its image missing on disk** (a new set before the images were fetched): its image route answers 404, so the board can fall back to the placeholder. The catalog itself is unaffected. Pinned in Task 2 (same test as item 1).
5. **The catalog read back by a client** has the same values as the server's card data, enums as names, empty power lists as `[]`. Pinned in Task 2 (`The_catalog_lists_every_card_and_printing`).

---

## File Structure

```
tools/CromoBound.Importer/CardImageDownloader.cs        (new: downloads printing images into a folder)
tools/CromoBound.Importer/ImporterApp.cs                (modify: the images command)
tests/CromoBound.Models.Tests/CardImageTests.cs         (new)
src/CromoBound.Contracts/CardContracts.cs               (new: CardCatalog, CatalogCard, CatalogPrinting)
src/CromoBound.Server/ServerOptions.cs                  (modify: CardImagesPath)
src/CromoBound.Server/Cards/CardEndpoints.cs            (new: the catalog, its builder, the image route)
src/CromoBound.Server/Program.cs                        (modify: AddCromoBoundCards, MapCards)
tests/CromoBound.Server.Tests/CardEndpointsTests.cs     (new)
Dockerfile                                              (modify: CardImagesPath and its folder)
docs/server-deploy.md                                   (modify: the image folder and how to fill it)
```

---

### Task 1: The importer downloads card images

**Files:**
- Create: `tools/CromoBound.Importer/CardImageDownloader.cs`, `tests/CromoBound.Models.Tests/CardImageTests.cs`
- Modify: `tools/CromoBound.Importer/ImporterApp.cs`

**Interfaces:**
- Consumes: `CardRepository.Load(string dataDir)` (`CromoBound.Data`), whose `Printings` values carry `Id` and `ImageUrl`.
- Produces:
  - `ImageSource(string PrintingId, string Url)`;
  - `ImageDownloadReport(int Downloaded, int Skipped, IReadOnlyList<string> Failed)`;
  - `CardImageDownloader(HttpClient http)`, with `const int MaxAtOnce = 4` and `Task<ImageDownloadReport> DownloadAsync(IReadOnlyList<ImageSource> images, string folder, CancellationToken cancel = default)`;
  - the command `dotnet run --project tools/CromoBound.Importer -- images <folder>`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Models.Tests/CardImageTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Headers;
using CromoBound.Importer;

namespace CromoBound.Models.Tests;

public class CardImageTests
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private static HttpResponseMessage Image(byte[] bytes, string mediaType = "image/png")
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private static CardImageDownloader Downloader(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://img.example.test/") });

    private static ImageSource Source(string id) => new(id, $"https://img.example.test/{id}.png?tag=x");

    [Fact]
    public async Task Png_answers_are_saved_under_the_printing_id()
    {
        using var dir = new TempDir();
        var handler = new FakeHandler(_ => Image(Png));

        var report = await Downloader(handler).DownloadAsync([Source("a1"), Source("b2")], dir.Root);

        Assert.Equal((2, 0), (report.Downloaded, report.Skipped));
        Assert.Empty(report.Failed);
        Assert.Equal(Png, await File.ReadAllBytesAsync(Path.Combine(dir.Root, "a1.png")));
        Assert.Equal(Png, await File.ReadAllBytesAsync(Path.Combine(dir.Root, "b2.png")));
    }

    [Fact]
    public async Task Files_already_there_are_skipped()
    {
        using var dir = new TempDir();
        dir.Write("a1.png", "kept");
        var handler = new FakeHandler(_ => Image(Png));

        var report = await Downloader(handler).DownloadAsync([Source("a1"), Source("b2")], dir.Root);

        Assert.Equal((1, 1), (report.Downloaded, report.Skipped));
        Assert.Equal(new[] { "/b2.png?tag=x" }, handler.Requests);
        Assert.Equal("kept", await File.ReadAllTextAsync(Path.Combine(dir.Root, "a1.png")));
    }

    [Fact]
    public async Task Failed_downloads_leave_no_file_and_are_reported()
    {
        using var dir = new TempDir();
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath switch
        {
            "/html.png" => Image("<html></html>"u8.ToArray(), "text/html"),
            "/missing.png" => new HttpResponseMessage(HttpStatusCode.NotFound),
            "/empty.png" => Image([]),
            "/dropped.png" => throw new HttpRequestException("The connection was reset."),
            _ => Image(Png),
        });

        var report = await Downloader(handler).DownloadAsync(
            [Source("html"), Source("missing"), Source("empty"), Source("dropped"), Source("good")], dir.Root);

        Assert.Equal((1, 0), (report.Downloaded, report.Skipped));
        Assert.Equal(new[] { "dropped", "empty", "html", "missing" }, report.Failed);
        Assert.Equal(new[] { "good.png" }, Directory.GetFiles(dir.Root).Select(Path.GetFileName));
    }

    [Fact]
    public async Task At_most_four_downloads_run_at_once()
    {
        using var dir = new TempDir();
        var handler = new SlowHandler(Png);

        var report = await Downloader(handler).DownloadAsync([.. Enumerable.Range(0, 12).Select(i => Source($"p{i}"))], dir.Root);

        Assert.Equal(12, report.Downloaded);
        Assert.InRange(handler.MostAtOnce, 2, CardImageDownloader.MaxAtOnce);
    }

    /// <summary>Answers after a short delay and records how many requests were open at the same time.</summary>
    private sealed class SlowHandler(byte[] bytes) : HttpMessageHandler
    {
        private int _open;

        public int MostAtOnce { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var open = Interlocked.Increment(ref _open);
            lock (this) MostAtOnce = Math.Max(MostAtOnce, open);
            await Task.Delay(30, cancellationToken);
            Interlocked.Decrement(ref _open);
            return Image(bytes);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`CardImageDownloader`, `ImageSource` and `ImageDownloadReport` don't exist).

- [ ] **Step 3: Write the implementation**

Create `tools/CromoBound.Importer/CardImageDownloader.cs`:

```csharp
using System.Collections.Concurrent;

namespace CromoBound.Importer;

/// <summary>One printing's image and where it comes from.</summary>
public sealed record ImageSource(string PrintingId, string Url);

/// <summary>What a run did: images saved, files already there, and the printings whose download failed (sorted).</summary>
public sealed record ImageDownloadReport(int Downloaded, int Skipped, IReadOnlyList<string> Failed);

/// <summary>Downloads printing images into one folder as <c>{printingId}.png</c> (spec §3.1). A file already there is skipped, so a
/// run resumes and only fetches what is new. At most <see cref="MaxAtOnce"/> downloads run at once. Only a successful, non-empty PNG
/// answer is saved, and it is written to a temporary file first, so a failed download never leaves a file behind.</summary>
public sealed class CardImageDownloader(HttpClient http)
{
    public const int MaxAtOnce = 4;

    public async Task<ImageDownloadReport> DownloadAsync(IReadOnlyList<ImageSource> images, string folder, CancellationToken cancel = default)
    {
        Directory.CreateDirectory(folder);
        var downloaded = 0;
        var skipped = 0;
        var failed = new ConcurrentBag<string>();
        using var gate = new SemaphoreSlim(MaxAtOnce);
        await Task.WhenAll(images.Select(async image =>
        {
            var target = Path.Combine(folder, image.PrintingId + ".png");
            if (File.Exists(target))
            {
                Interlocked.Increment(ref skipped);
                return;
            }
            await gate.WaitAsync(cancel);
            try
            {
                if (await TryDownloadAsync(image.Url, target, cancel)) Interlocked.Increment(ref downloaded);
                else failed.Add(image.PrintingId);
            }
            finally
            {
                gate.Release();
            }
        }));
        return new ImageDownloadReport(downloaded, skipped, [.. failed.Order(StringComparer.Ordinal)]);
    }

    private async Task<bool> TryDownloadAsync(string url, string target, CancellationToken cancel)
    {
        var part = target + ".part";
        try
        {
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
            if (!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType != "image/png") return false;
            await using (var file = File.Create(part)) await response.Content.CopyToAsync(file, cancel);
            if (new FileInfo(part).Length == 0) return false;
            File.Move(part, target);
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException || (ex is TaskCanceledException && !cancel.IsCancellationRequested))
        {
            return false;
        }
        finally
        {
            if (File.Exists(part)) File.Delete(part);
        }
    }
}
```

A timeout surfaces as a `TaskCanceledException` while `cancel` isn't cancelled. It counts as one failed image. A real cancellation still stops the run.

In `tools/CromoBound.Importer/ImporterApp.cs`:

1. Replace the `Usage` constant with:

```csharp
    private const string Usage = "Usage: dotnet run --project tools/CromoBound.Importer -- <fetch|normalize|schema|all|images <folder>>";
```

2. Add a case to the `switch`, before `default`:

```csharp
            case "images" when args.Length == 2:
                return await ImagesAsync(dataDir, args[1]);
```

3. Add the method:

```csharp
    /// <summary>Downloads every printing's image into the folder (spec §3.1); fails when any image couldn't be fetched, so a deploy
    /// script notices. Running it again fetches only what is missing.</summary>
    private static async Task<int> ImagesAsync(string dataDir, string folder)
    {
        var printings = CardRepository.Load(dataDir).Printings.Values;
        var sources = printings.Where(p => !string.IsNullOrEmpty(p.ImageUrl)).Select(p => new ImageSource(p.Id, p.ImageUrl!)).ToList();
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("CromoBound-Importer/1.0");
        var report = await new CardImageDownloader(http).DownloadAsync(sources, Path.GetFullPath(folder));
        Console.WriteLine($"Images: {report.Downloaded} downloaded, {report.Skipped} already there, {report.Failed.Count} failed, "
            + $"{printings.Count - sources.Count} printings without an image link.");
        foreach (var id in report.Failed) Console.Error.WriteLine($"Failed: {id}");
        return report.Failed.Count == 0 ? 0 : 1;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, the four new ones included).

Do not run the real `images` command: it downloads about 1451 files from the internet. The owner runs it when deploying (Task 3).

- [ ] **Step 5: Commit**

```bash
git add tools/CromoBound.Importer tests/CromoBound.Models.Tests/CardImageTests.cs
git commit -m "feat(importer): download the card images"
```

---

### Task 2: The server serves the catalog and the images

**Files:**
- Create: `src/CromoBound.Contracts/CardContracts.cs`, `src/CromoBound.Server/Cards/CardEndpoints.cs`, `tests/CromoBound.Server.Tests/CardEndpointsTests.cs`
- Modify: `src/CromoBound.Server/ServerOptions.cs`, `src/CromoBound.Server/Program.cs`

**Interfaces:**
- Consumes:
  - `CardDatabase` (singleton in the server, from `AddCromoBoundMatches`), with `Cards` (tokens included, `Supertype.Token`) and `Printings`;
  - `WireJson.Options`, `Policies.Seat`, `ServerOptions`.
- Produces:
  - in `CromoBound.Contracts`: `CardCatalog(IReadOnlyList<CatalogCard> Cards, IReadOnlyList<CatalogPrinting> Printings)`;
  - `CatalogCard(string Id, string Name, CardType Type, Supertype? Supertype, IReadOnlyList<Domain> Domains, int? Energy, IReadOnlyList<PowerSymbol> Power, int? Might)`;
  - `CatalogPrinting(string Id, string CardId, Orientation Orientation)`;
  - `ServerOptions.CardImagesPath` (default `"card-images"`);
  - `CardEndpoints.AddCromoBoundCards(IServiceCollection)`, `CardEndpoints.MapCards(IEndpointRouteBuilder)` and `CardEndpoints.CatalogOf(CardDatabase)`;
  - the routes `GET /api/cards` and `GET /cards/img/{printingId}`.

- [ ] **Step 1: Write the failing tests**

Create `tests/CromoBound.Server.Tests/CardEndpointsTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using CromoBound.Contracts;
using CromoBound.Models.Cards;

namespace CromoBound.Server.Tests;

public class CardEndpointsTests
{
    private static async Task<HttpClient> PlayerAsync(ServerFactory factory)
    {
        await factory.AddUserAsync("player1");
        return await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
    }

    private static string NewImageFolder() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), $"cromobound-images-{Guid.NewGuid():N}")).FullName;

    [Fact]
    public async Task Signed_out_the_card_routes_are_a_bare_401()
    {
        using var factory = new ServerFactory();
        var client = factory.NewClient();
        var printing = ServerFactory.TestCards.Printings.Keys.First();

        foreach (var route in new[] { "/api/cards", $"/cards/img/{printing}" })
        {
            var response = await client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task The_catalog_lists_every_card_and_printing()
    {
        using var factory = new ServerFactory();
        var player = await PlayerAsync(factory);

        var response = await player.GetAsync("/api/cards");
        var catalog = await response.Content.ReadFromJsonAsync<CardCatalog>(WireJson.Options);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.NotNull(catalog);
        var cards = ServerFactory.TestCards.Cards.Values;
        Assert.Equal(cards.Select(c => c.Id).Order(StringComparer.Ordinal), catalog.Cards.Select(c => c.Id));
        Assert.Equal(ServerFactory.TestCards.Printings.Keys.Order(StringComparer.Ordinal), catalog.Printings.Select(p => p.Id));
        foreach (var card in cards)
        {
            var entry = catalog.Cards.Single(c => c.Id == card.Id);
            Assert.Equal((card.Name, card.Type, card.Supertype, card.Might, card.Cost?.Energy),
                (entry.Name, entry.Type, entry.Supertype, entry.Might, entry.Energy));
            Assert.Equal(card.Domains, entry.Domains);
            Assert.Equal(card.Cost?.Power ?? [], entry.Power);
        }
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"type\":\"Unit\"", json);
        Assert.Contains("\"power\":[]", json);
    }

    [Fact]
    public async Task An_image_is_served_as_png_with_a_long_private_cache()
    {
        var folder = NewImageFolder();
        var printing = ServerFactory.TestCards.Printings.Keys.First();
        byte[] bytes = [0x89, 0x50, 0x4E, 0x47, 7, 7, 7];
        await File.WriteAllBytesAsync(Path.Combine(folder, printing + ".png"), bytes);
        using var factory = new ServerFactory(new() { ["CromoBound:CardImagesPath"] = folder });
        var player = await PlayerAsync(factory);

        var response = await player.GetAsync($"/cards/img/{printing}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("private, max-age=604800", response.Headers.CacheControl?.ToString());
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        Directory.Delete(folder, recursive: true);
    }

    [Fact]
    public async Task Unknown_printings_traversal_and_missing_files_are_not_found()
    {
        var folder = NewImageFolder();
        var outside = Path.Combine(Path.GetDirectoryName(folder)!, $"outside-{Guid.NewGuid():N}.png");
        await File.WriteAllTextAsync(outside, "secret");
        var printing = ServerFactory.TestCards.Printings.Keys.First();
        using var factory = new ServerFactory(new() { ["CromoBound:CardImagesPath"] = folder });
        var player = await PlayerAsync(factory);

        var paths = new[]
        {
            "/cards/img/not-a-printing",
            $"/cards/img/..%2F{Path.GetFileNameWithoutExtension(outside)}",
            $"/cards/img/{Uri.EscapeDataString(Path.GetFileNameWithoutExtension(outside))}",
            $"/cards/img/{printing}",
        };
        foreach (var path in paths)
        {
            var response = await player.GetAsync(path);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("secret", await response.Content.ReadAsStringAsync());
        }
        File.Delete(outside);
        Directory.Delete(folder, recursive: true);
    }
}
```

`DefaultDenyTests` already walks every endpoint and checks that each one requires a role, so the two new routes are covered without a new test there.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet build CromoBound.slnx --no-incremental`
Expected: FAIL to compile (`CardCatalog` doesn't exist).

- [ ] **Step 3: Write the implementation**

Create `src/CromoBound.Contracts/CardContracts.cs`:

```csharp
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Contracts;

/// <summary>What the client needs to draw cards (spec §3.3): names, costs and might for placeholders and menus, and which card each
/// printing shows. No rules text: the image carries it. Tokens are cards with the Token supertype; they have no printings.</summary>
public sealed record CardCatalog(IReadOnlyList<CatalogCard> Cards, IReadOnlyList<CatalogPrinting> Printings);

public sealed record CatalogCard(
    string Id, string Name, CardType Type, Supertype? Supertype, IReadOnlyList<Domain> Domains, int? Energy,
    IReadOnlyList<PowerSymbol> Power, int? Might);

public sealed record CatalogPrinting(string Id, string CardId, Orientation Orientation);
```

In `src/CromoBound.Server/ServerOptions.cs`, add after `DataFolder`:

```csharp
    /// <summary>The folder of card images, <c>{printingId}.png</c>, filled by the importer's <c>images</c> command (spec §3).</summary>
    public string CardImagesPath { get; set; } = "card-images";
```

Create `src/CromoBound.Server/Cards/CardEndpoints.cs`:

```csharp
using CromoBound.Contracts;
using CromoBound.Data;
using CromoBound.Server.Accounts;
using Microsoft.Extensions.Options;

namespace CromoBound.Server.Cards;

/// <summary>The card catalog and the card images (spec §3), for signed-in players only. The catalog is built once from the card
/// database; like every <c>/api</c> answer it is never stored by a cache. An image is served only for a printing the database knows, from the image folder, so no other file can be reached.</summary>
internal static class CardEndpoints
{
    public const string ImageCache = "private, max-age=604800";

    /// <summary>The catalog, built from the card data the matches use. Call after <c>AddCromoBoundMatches</c>.</summary>
    public static IServiceCollection AddCromoBoundCards(this IServiceCollection services) =>
        services.AddSingleton(provider => CatalogOf(provider.GetRequiredService<CardDatabase>()));

    public static void MapCards(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/cards", (CardCatalog catalog) => Results.Json(catalog, WireJson.Options)).RequireAuthorization(Policies.Seat);
        app.MapGet("/cards/img/{printingId}", Image).RequireAuthorization(Policies.Seat);
    }

    public static CardCatalog CatalogOf(CardDatabase cards) => new(
        [.. cards.Cards.Values.OrderBy(c => c.Id, StringComparer.Ordinal).Select(c => new CatalogCard(
            c.Id, c.Name, c.Type, c.Supertype, c.Domains, c.Cost?.Energy, c.Cost?.Power ?? [], c.Might))],
        [.. cards.Printings.Values.OrderBy(p => p.Id, StringComparer.Ordinal).Select(p => new CatalogPrinting(p.Id, p.CardId, p.Orientation))]);

    private static IResult Image(string printingId, CardDatabase cards, IOptions<ServerOptions> options, HttpContext http)
    {
        if (!cards.Printings.ContainsKey(printingId)) return Results.NotFound();
        var path = Path.Combine(Path.GetFullPath(options.Value.CardImagesPath), printingId + ".png");
        if (!File.Exists(path)) return Results.NotFound();
        http.Response.Headers.CacheControl = ImageCache;
        return Results.File(path, "image/png");
    }
}
```

`Results.File` with an absolute path serves the file from disk. The id is looked up in the card database before any path is built, so `..`, slashes or encoded characters never reach the file system.

In `src/CromoBound.Server/Program.cs`:

1. Add `using CromoBound.Server.Cards;` with the other usings.
2. After `builder.Services.AddCromoBoundMatches();` add:

```csharp
builder.Services.AddCromoBoundCards();
```

3. After `app.MapMaintenance();` add:

```csharp
app.MapCards();
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet build CromoBound.slnx --no-incremental` (expect `Avvisi: 0`, `Errori: 0`), then `dotnet test CromoBound.slnx`.
Expected: PASS (all tests, the four new ones and `DefaultDenyTests` included).

- [ ] **Step 5: Commit**

```bash
git add src/CromoBound.Contracts src/CromoBound.Server tests/CromoBound.Server.Tests/CardEndpointsTests.cs
git commit -m "feat(server): serve the card catalog and the card images"
```

---

### Task 3: The image folder in the Docker image and the deploy guide

**Files:**
- Modify: `Dockerfile`, `docs/server-deploy.md`

**Interfaces:**
- Consumes:
  - Task 1's command `dotnet run --project tools/CromoBound.Importer -- images <folder>`;
  - Task 2's setting `CromoBound__CardImagesPath`.
- Produces: the image folder at `/var/lib/cromobound/images` in the container, mounted read-only from `/opt/cromobound/card-images` on the VPS.

- [ ] **Step 1: Update the Dockerfile**

In `Dockerfile`, add a line to the `ENV` instruction, after the `CromoBound__KeysFolder` line, so the instruction reads:

```dockerfile
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    CromoBound__DataFolder=/app/data \
    CromoBound__DatabasePath=/var/lib/cromobound/db/cromobound.db \
    CromoBound__KeysFolder=/var/lib/cromobound/keys \
    CromoBound__CardImagesPath=/var/lib/cromobound/images
```

and replace the `RUN mkdir` line with:

```dockerfile
RUN mkdir -p /var/lib/cromobound/db /var/lib/cromobound/keys /var/lib/cromobound/images && chown -R $APP_UID /var/lib/cromobound
```

- [ ] **Step 2: Update the deploy guide**

In `docs/server-deploy.md`:

1. In section 3's `compose.yaml`, add a third line under the app's `volumes:`, so it reads:

```yaml
    volumes:
      - db:/var/lib/cromobound/db
      - keys:/var/lib/cromobound/keys
      - ./card-images:/var/lib/cromobound/images:ro
```

2. In section 4's settings table, add a row after the `CromoBound__DataFolder` row:

```markdown
| `CromoBound__CardImagesPath` | `/var/lib/cromobound/images` | The card images, mounted read-only from `/opt/cromobound/card-images` (section 5) |
```

3. In section 5 ("First start"), add this before the `docker compose up -d` block:

````markdown
Fill the card images folder once, from the repository clone. The importer runs in a throwaway SDK container and downloads every
printing's image (about 1450 files) into `/opt/cromobound/card-images`:

```bash
mkdir -p /opt/cromobound/card-images
docker run --rm -v "$PWD":/src -v /opt/cromobound/card-images:/out -w /src mcr.microsoft.com/dotnet/sdk:10.0 \
  dotnet run --project tools/CromoBound.Importer -- images /out
```

The command can be run again at any time: it fetches only the images that are missing, and says which ones failed. A card whose
image is missing still shows on the board, as a placeholder with its name.
````

4. In section 6 ("Deploying a new version"), add a step after step 3:

```markdown
4. If the new version adds cards (a new set), fill the card images again with the command in section 5. The app doesn't need a
   restart for new images.
```

5. In section 8's checklist, add after the volumes line:

```markdown
- [ ] `/opt/cromobound/card-images` is filled and mounted read-only.
```

- [ ] **Step 3: Check the text**

Run: `LC_ALL=C.UTF-8 grep -nP '[\x{2013}\x{2014}]' Dockerfile docs/server-deploy.md` (expect no output), and `grep -c $'\r' Dockerfile docs/server-deploy.md` (expect `0` for both).

- [ ] **Step 4: Commit**

```bash
git add Dockerfile docs/server-deploy.md
git commit -m "docs(deploy): add the card images folder"
```

---

## Done criteria

- `dotnet build CromoBound.slnx --no-incremental` reports 0 warnings and 0 errors, and `dotnet test CromoBound.slnx` passes, the new importer and server tests included.
- `dotnet run --project tools/CromoBound.Importer -- images <folder>` downloads every printing's image, skips what is there, and reports failures (the owner runs it; the tests cover it with a fake handler).
- `GET /api/cards` and `GET /cards/img/{printingId}` answer signed-in players and give a bare 401 when signed out. An unknown id, a traversal attempt and a missing file each give a 404.
- The Docker image and the deploy guide include the image folder.
