using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Importer;

public sealed class ImportException(string message) : Exception(message);

public enum ConflictKind { ReminderOnly, Wording }

public sealed record TextConflict(string CardId, ConflictKind Kind, IReadOnlyList<string> Texts);

public sealed class ImportReport
{
    public List<TextConflict> TextConflicts { get; } = [];
    public SortedSet<string> MissingPowerDomains { get; } = new(StringComparer.Ordinal);
    /// <summary>Unknown bracket keyword → first card id it was seen on (or "(keywords index)").</summary>
    public SortedDictionary<string, string> UnknownKeywords { get; } = new(StringComparer.Ordinal);
    public SortedSet<string> MissingTokens { get; } = new(StringComparer.Ordinal);
    public SortedDictionary<CardType, int> TypeCounts { get; } = new();
    public SortedDictionary<MappingStatus, int> StatusCounts { get; } = new();
}
