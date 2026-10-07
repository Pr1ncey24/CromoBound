namespace CromoBound.Models.Effects;

public sealed record ChoosePlayerStep : Step
{
    public Filter? Filter { get; init; }
}

/// <summary>Choose card(s) from a zone without targeting (hidden zones, trash picks).</summary>
public sealed record ChooseCardStep : Step
{
    public required ZoneRef From { get; init; }
    public Filter? Filter { get; init; }
    public Value Count { get; init; } = 1;
}

public sealed record NameTagStep : Step
{
    /// <summary>Allowed tags. Empty means any tag from the tag list.</summary>
    public IReadOnlyList<string> Options { get; init; } = [];
}

public sealed record NameCardStep : Step
{
    public Filter? Filter { get; init; }
}
