using CromoBound.Engine.State;

namespace CromoBound.Engine.Actions;

/// <summary>Bo3: pick one of your unused battlefields (by printing id).</summary>
public sealed record PickBattlefield(string Printing) : PlayerAction;

public sealed record ChoosePlayOrder(bool First) : PlayerAction;

/// <summary>One copy of <see cref="Out"/> goes from the main deck to the sideboard; one copy of <see cref="In"/> comes back.</summary>
public sealed record SideboardSwap(string Out, string In);

/// <summary>Sideboard swaps (empty = no changes), and optionally a new Chosen Champion taken from the main deck or sideboard.</summary>
public sealed record SubmitSideboard : PlayerAction
{
    public IReadOnlyList<SideboardSwap> Swaps { get; init; } = [];
    public string? Champion { get; init; }
}

/// <summary>Set aside up to 2 cards: draw that many, then recycle them to the bottom in random order (CR 117).</summary>
public sealed record Mulligan : PlayerAction
{
    public IReadOnlyList<ObjectId> SetAside { get; init; } = [];
}

/// <summary>Ask to roll back to just before your last action; the opponent must agree.</summary>
public sealed record RequestUndo : PlayerAction;

public sealed record AnswerUndo(bool Accept) : PlayerAction;

/// <summary>Concede the current game; the opponent wins it.</summary>
public sealed record Concede : PlayerAction;
