using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Contracts;

/// <summary>What the client needs to draw cards (spec §3.3): names, costs and might for placeholders and menus, and which card each
/// printing shows, and each card's standard art (<c>DefaultPrintingId</c>) for when only the card is known. No rules text: the image carries it. Tokens are cards with the Token supertype; they have no printings.</summary>
public sealed record CardCatalog(IReadOnlyList<CatalogCard> Cards, IReadOnlyList<CatalogPrinting> Printings);

public sealed record CatalogCard(
    string Id, string Name, CardType Type, Supertype? Supertype, IReadOnlyList<Domain> Domains, int? Energy,
    IReadOnlyList<PowerSymbol> Power, int? Might, string? DefaultPrintingId);

public sealed record CatalogPrinting(string Id, string CardId, Orientation Orientation);
