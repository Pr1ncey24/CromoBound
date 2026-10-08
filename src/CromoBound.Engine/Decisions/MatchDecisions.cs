using CromoBound.Engine.State;

namespace CromoBound.Engine.Decisions;

public sealed record BattlefieldChoice(PlayerId Player, IReadOnlyList<string> Printings);

/// <summary>Bo3: every listed player picks one of their unused battlefields, in any order; picks stay hidden until all are in.</summary>
public sealed record PickBattlefieldDecision(IReadOnlyList<PlayerId> Players, IReadOnlyList<BattlefieldChoice> Choices) : PendingDecision(Players);

public sealed record ChoosePlayOrderDecision(PlayerId Player) : PendingDecision([Player]);

/// <summary>Every listed player submits their sideboard swaps (or none), in any order.</summary>
public sealed record SideboardDecision(IReadOnlyList<PlayerId> Players) : PendingDecision(Players);

public sealed record MulliganDecision(PlayerId Player, IReadOnlyList<ObjectId> Hand) : PendingDecision([Player]);

public sealed record ConfirmUndoDecision(PlayerId Player, PlayerId RequestedBy) : PendingDecision([Player]);
