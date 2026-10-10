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
