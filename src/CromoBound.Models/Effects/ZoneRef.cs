namespace CromoBound.Models.Effects;

public sealed record ZoneRef
{
    public required Zone Zone { get; init; }
    public PlayerRef? Owner { get; init; }
    public DeckPosition? Position { get; init; }
}
