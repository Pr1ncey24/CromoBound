using CromoBound.Engine.Actions;
using CromoBound.Engine.State;
using CromoBound.Engine.Views;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Board;

/// <summary>How a card is marked: a legal choice, the engine's suggestion, or chosen by the player.</summary>
public enum Ring { None, Legal, Suggested, Selected }

/// <summary>What a rune does in the payment being made.</summary>
public enum PayMark { None, Exhaust, Recycle, Both }

/// <summary>How a rune pays: exhausted for energy, recycled for power, or both (exhausted, then recycled).</summary>
public enum RunePay { Exhaust, Recycle, Both }

/// <summary>A card as the board draws it: the view's state, the catalog's name, cost and printed might, and the board's marks.</summary>
public sealed record BoardCard(
    ObjectId Id, string CardId, string? PrintingId, string Name, CardType? Type, int? Cost, int? PrintedMight,
    bool Exhausted, bool Stunned, bool Buffed, bool Empowered, int Damage, int? Might, int Gear,
    bool Clickable = false, Ring Ring = Ring.None, PayMark Mark = PayMark.None)
{
    public bool MightChanged => Might is not null && PrintedMight is not null && Might != PrintedMight;
}

/// <summary>One player's side. The opponent's hand is only a count.</summary>
public sealed record BoardSide(
    PlayerId Player, string Name, bool IsMe, int Points, int Xp, PoolView Pool,
    BoardCard? Legend, BoardCard? Champion, IReadOnlyList<BoardCard> Base, IReadOnlyList<BoardCard> Runes,
    IReadOnlyList<BoardCard> Hand, int HandCount, int MainDeckCount, int RuneDeckCount,
    BoardCard? TrashTop, int TrashCount, IReadOnlyList<BoardCard> Banished, bool BaseIsDestination);

/// <summary>A battlefield lane: its card, who holds it, the units on each side, and whether a move may end here.</summary>
public sealed record BoardLane(
    int Index, BoardCard Card, PlayerId? Controller, bool Contested, IReadOnlyList<BoardCard> Mine, IReadOnlyList<BoardCard> Theirs,
    bool HasHidden, BoardCard? Hidden, bool IsDestination);

public sealed record ChainRow(int Id, string Name, string Controller);

/// <summary>What a click does: send an action, change the local interaction, open a card's menu, or nothing.</summary>
public abstract record BoardStep;

public sealed record SendStep(PlayerAction Action) : BoardStep;

public sealed record NextStep(Interaction Next) : BoardStep;

public sealed record MenuStep(ObjectId Card, IReadOnlyList<MenuItem> Items) : BoardStep;

public sealed record NoStep : BoardStep
{
    public static NoStep Instance { get; } = new();
}

public sealed record MenuItem(string Label, BoardStep Step);

/// <summary>A button of the match panel; it is enabled when it does something.</summary>
public sealed record BoardButton(string Label, string? Detail, BoardStep Step)
{
    public bool Enabled => Step is not NoStep;
}

/// <summary>What the player is in the middle of, kept by the page between views.</summary>
public abstract record Interaction;

public sealed record Idle : Interaction
{
    public static Idle Instance { get; } = new();
}

/// <summary>Moving these units; they pick a destination next.</summary>
public sealed record Moving(IReadOnlyList<ObjectId> Units) : Interaction;

/// <summary>Paying, with the use the player chose for each rune (runes not listed are unused).</summary>
public sealed record Paying(IReadOnlyDictionary<ObjectId, RunePay> Uses) : Interaction;

/// <summary>A panel over the board: a pre-game step, a prompt, or the undo answer. Tasks 2 and 3 add the kinds.</summary>
public abstract record BoardPanel;
