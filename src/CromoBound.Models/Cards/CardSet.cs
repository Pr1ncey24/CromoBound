namespace CromoBound.Models.Cards;

public sealed record CardSet
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public DateOnly? PublishedOn { get; init; }
    public int CardCount { get; init; }
}
