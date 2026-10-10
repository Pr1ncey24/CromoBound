using CromoBound.Engine.State;

namespace CromoBound.Client.Board;

/// <summary>Where a card being played enters, and whether Accelerate is on offer.</summary>
public sealed record PlayOptionsPanel(string CardName, IReadOnlyList<PlaceOption> Locations, bool AccelerateAvailable) : BoardPanel;

public sealed record PlaceOption(Place Place, string Label);
