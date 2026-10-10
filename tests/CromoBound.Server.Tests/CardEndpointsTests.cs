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
        foreach (var printing in ServerFactory.TestCards.Printings.Values)
        {
            var entry = catalog.Printings.Single(p => p.Id == printing.Id);
            Assert.Equal((printing.CardId, printing.Orientation), (entry.CardId, entry.Orientation));
        }
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
        Assert.True(response.Headers.CacheControl?.Private);
        Assert.Equal(TimeSpan.FromDays(7), response.Headers.CacheControl?.MaxAge);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
        var lastModified = response.Content.Headers.LastModified;
        Assert.NotNull(lastModified);

        using var revalidation = new HttpRequestMessage(HttpMethod.Get, $"/cards/img/{printing}");
        revalidation.Headers.IfModifiedSince = lastModified;
        var again = await player.SendAsync(revalidation);
        Assert.Equal(HttpStatusCode.NotModified, again.StatusCode);
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
