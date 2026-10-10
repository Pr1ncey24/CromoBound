using CromoBound.Contracts;

namespace CromoBound.Client.Board;

/// <summary>Cards and printings by id, over the server's catalog. A card the catalog doesn't know reads as "a card", so a board drawn
/// from a newer view than the catalog still works.</summary>
public sealed class CardBook(CardCatalog catalog)
{
    public const string Unknown = "a card";

    private readonly Dictionary<string, CatalogCard> _cards = catalog.Cards.ToDictionary(c => c.Id, StringComparer.Ordinal);
    private readonly Dictionary<string, CatalogPrinting> _printings = catalog.Printings.ToDictionary(p => p.Id, StringComparer.Ordinal);

    public CatalogCard? Card(string cardId) => _cards.GetValueOrDefault(cardId);

    public string NameOf(string cardId) => Card(cardId)?.Name ?? Unknown;

    public CatalogPrinting? PrintingOf(string printingId) => _printings.GetValueOrDefault(printingId);

    public string? DefaultPrinting(string cardId) => Card(cardId)?.DefaultPrintingId;
}
