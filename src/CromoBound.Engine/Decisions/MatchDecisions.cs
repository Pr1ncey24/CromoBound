using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Json;

namespace CromoBound.Engine.Decisions;

public sealed record BattlefieldChoice(PlayerId Player, IReadOnlyList<string> Printings);

/// <summary>Bo3: every listed player picks one of their unused battlefields, in any order; picks stay hidden until all are in.</summary>
public sealed record PickBattlefieldDecision(IReadOnlyList<PlayerId> Players, IReadOnlyList<BattlefieldChoice> Choices) : PendingDecision(Players);

public sealed record ChoosePlayOrderDecision(PlayerId Player) : PendingDecision([Player]);

/// <summary>A player's deck as it stands: what they may swap and which cards may become the Chosen Champion (spec §6.3).</summary>
public sealed record SideboardChoice(
    PlayerId Player,
    [property: KeepEmpty] IReadOnlyList<DeckEntry> Main,
    [property: KeepEmpty] IReadOnlyList<DeckEntry> Sideboard,
    string Champion,
    [property: KeepEmpty] IReadOnlyList<string> ChampionCandidates);

/// <summary>Every listed player submits their sideboard swaps (or none), in any order.</summary>
public sealed record SideboardDecision(IReadOnlyList<PlayerId> Players, [property: KeepEmpty] IReadOnlyList<SideboardChoice> Choices) : PendingDecision(Players);

public sealed record MulliganDecision(PlayerId Player, IReadOnlyList<ObjectId> Hand) : PendingDecision([Player]);

public sealed record ConfirmUndoDecision(PlayerId Player, PlayerId RequestedBy) : PendingDecision([Player]);
