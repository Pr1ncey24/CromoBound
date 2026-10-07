using System.Text.Json;
using System.Text.Json.Serialization;

namespace CromoBound.Importer;

/// <summary>Riftcodex API JSON (snake_case). Unknown fields are ignored so API additions don't break the import.</summary>
public static class RawJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
    };
}

public sealed record RawCard
{
    public required string Id { get; init; }
    public string Name { get; init; } = "";
    public string? RiftboundId { get; init; }
    public string? TcgplayerId { get; init; }
    public int? CollectorNumber { get; init; }
    public RawAttributes Attributes { get; init; } = new();
    public RawClassification Classification { get; init; } = new();
    public RawText Text { get; init; } = new();
    public RawSetRef Set { get; init; } = new();
    public RawMedia Media { get; init; } = new();
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? Orientation { get; init; }
    public RawMetadata Metadata { get; init; } = new();
}

public sealed record RawAttributes
{
    public int? Energy { get; init; }
    public int? Might { get; init; }
    public int? Power { get; init; }
}

public sealed record RawClassification
{
    public string? Type { get; init; }
    public string? Supertype { get; init; }
    public string? Rarity { get; init; }
    public IReadOnlyList<string> Domain { get; init; } = [];
}

public sealed record RawText
{
    public string? Rich { get; init; }
    public string? Plain { get; init; }
    public string? Flavour { get; init; }
}

public sealed record RawSetRef
{
    public string SetId { get; init; } = "";
    public string? Label { get; init; }
}

public sealed record RawMedia
{
    public string? ImageUrl { get; init; }
    public string? Artist { get; init; }
}

public sealed record RawMetadata
{
    public bool AlternateArt { get; init; }
    public bool Overnumbered { get; init; }
    public bool Signature { get; init; }
}

public sealed record RawSet
{
    public required string SetId { get; init; }
    public required string Name { get; init; }
    public int CardCount { get; init; }
    public string? PublishedOn { get; init; }
}
