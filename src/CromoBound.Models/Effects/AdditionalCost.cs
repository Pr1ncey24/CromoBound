namespace CromoBound.Models.Effects;

/// <summary>An additional cost to play the card (Core Rules 356 step b). Mandatory unless Optional is true.</summary>
public sealed record AdditionalCost
{
    public required string Id { get; init; }
    public bool? Optional { get; init; }
    public required Cost Cost { get; init; }
    public IReadOnlyList<Step> OnPaid { get; init; } = [];
    public Modifier? ModifiesCost { get; init; }
}
