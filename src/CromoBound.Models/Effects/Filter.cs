using CromoBound.Models.Cards;

namespace CromoBound.Models.Effects;

public sealed record Filter
{
    public Relation? Relation { get; init; }
    public PlayerRef? Controller { get; init; }
    public PlayerRef? Owner { get; init; }
    public ObjectRef? Location { get; init; }
    public Zone? Zone { get; init; }
    public CardType? Type { get; init; }
    public Supertype? Supertype { get; init; }
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<Domain> Domains { get; init; } = [];
    public string? Name { get; init; }
    public NumberFilter? Might { get; init; }
    public NumberFilter? EnergyCost { get; init; }
    public IReadOnlyList<ObjectStatus> Status { get; init; } = [];
    public bool? Mighty { get; init; }
    public bool? Token { get; init; }
    public MechanicalKeyword? Keyword { get; init; }
    public bool? Other { get; init; }
    public Filter? Not { get; init; }
}

public sealed record NumberFilter
{
    public Value? Eq { get; init; }
    public Value? Lte { get; init; }
    public Value? Gte { get; init; }
    public Value? Lt { get; init; }
    public Value? Gt { get; init; }
}
