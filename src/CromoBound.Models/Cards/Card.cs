using CromoBound.Models.Json;

namespace CromoBound.Models.Cards;

/// <summary>One gameplay identity. All printings of the same name share one Card.</summary>
public sealed record Card
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required CardType Type { get; init; }
    public Supertype? Supertype { get; init; }
    public IReadOnlyList<Domain> Domains { get; init; } = [];
    public CardCost? Cost { get; init; }
    public int? Might { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<DisplayKeyword> Keywords { get; init; } = [];
    public required CardText Text { get; init; }
    public string? DefaultPrintingId { get; init; }
}

public sealed record CardCost
{
    public int? Energy { get; init; }

    /// <summary>One entry per power symbol. <c>null</c> means unknown (multi-domain card without an override).</summary>
    [KeepEmpty]
    public IReadOnlyList<Domain>? Power { get; init; }
}

public sealed record CardText
{
    public string Rich { get; init; } = "";
    public string Plain { get; init; } = "";
}
