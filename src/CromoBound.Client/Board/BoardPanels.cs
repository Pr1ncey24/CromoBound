using CromoBound.Engine.State;

namespace CromoBound.Client.Board;

/// <summary>Where a card being played enters, and whether Accelerate is on offer.</summary>
public sealed record PlayOptionsPanel(string CardName, IReadOnlyList<PlaceOption> Locations, bool AccelerateAvailable) : BoardPanel;

public sealed record PlaceOption(Place Place, string Label);

/// <summary>A chain item's text the engine doesn't run: the controller carries it out by hand, then presses Done.</summary>
public sealed record ResolvePanel(string CardName, string? PrintingId, string Text) : BoardPanel;

/// <summary>Cards whose start- or end-of-turn text is applied by hand, then Continue.</summary>
public sealed record TurnPointPanel(string Title, IReadOnlyList<BoardCard> Cards) : BoardPanel;

/// <summary>The opponent asks to undo their last action.</summary>
public sealed record UndoPanel(string Requester, string? LastAction) : BoardPanel;

/// <summary>A decision a later part of 4b handles (spec §6.5).</summary>
public sealed record LaterPanel(string Kind, string What) : BoardPanel;

public sealed record PickBattlefieldPanel(int GameNumber, int Games, IReadOnlyList<PrintingOption> Options, bool Waiting, string Opponent) : BoardPanel;

public sealed record PrintingOption(string Printing, string Name);

public sealed record PlayOrderPanel(int GameNumber, bool Waiting, string Opponent) : BoardPanel;

public sealed record SideboardPanel(int GameNumber, IReadOnlyList<DeckRow> Main, IReadOnlyList<DeckRow> Sideboard, bool Waiting, string Opponent) : BoardPanel;

public sealed record DeckRow(string Printing, string Name, int Count);

public sealed record MulliganPanel(int GameNumber, IReadOnlyList<BoardCard> Hand, bool Waiting, string Opponent) : BoardPanel;
