using CromoBound.Engine.Actions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Decisions;

/// <summary>What the engine is waiting for and from whom.</summary>
public abstract record PendingDecision(IReadOnlyList<PlayerId> Players);

public sealed record RuneOption(ObjectId Rune, bool CanExhaust);

public sealed record MoveOption(ObjectId Unit, IReadOnlyList<Place> Destinations);

public sealed record HideOption(ObjectId Card, IReadOnlyList<int> Battlefields);

/// <summary>The priority (or focus) holder's options. Playable lists cards by timing only; payment is checked when paying.</summary>
public sealed record PriorityDecision(
    PlayerId Player,
    IReadOnlyList<ObjectId> Playable,
    IReadOnlyList<RuneOption> Runes,
    IReadOnlyList<MoveOption> Moves,
    IReadOnlyList<HideOption> Hides,
    bool CanPass,
    bool CanEndTurn) : PendingDecision([Player]);

public sealed record PlayChoicesDecision(PlayerId Player, ObjectId Card, IReadOnlyList<Place> Locations, bool AccelerateAvailable)
    : PendingDecision([Player]);

/// <summary>Energy plus one power symbol per entry.</summary>
public sealed record TotalCost(int Energy, IReadOnlyList<PowerSymbol> Power);

public sealed record PaymentSuggestion(IReadOnlyList<ObjectId> Exhaust, IReadOnlyList<ObjectId> Recycle);

/// <summary>Pay <see cref="Cost"/>. <see cref="Domains"/> are the card's domains (what [C]/Self accepts). The suggestion is only a shortcut.</summary>
public sealed record PayCostDecision(PlayerId Player, TotalCost Cost, IReadOnlyList<Domain> Domains, PaymentSuggestion? Suggested)
    : PendingDecision([Player]);

public sealed record ChooseShowdownDecision(PlayerId Player, IReadOnlyList<int> Battlefields, bool Combat) : PendingDecision([Player]);

public enum DamageGroup { Tank, Normal, Backline }

public sealed record DamageTarget(ObjectId Unit, int Lethal, DamageGroup Group);

public sealed record AssignDamageDecision(
    PlayerId Player, int Battlefield, int Total, IReadOnlyList<DamageTarget> Targets, IReadOnlyList<DamageAssignment> Suggested)
    : PendingDecision([Player]);

/// <summary>Carry out the item's effect with manual actions, then submit ResolveDone.</summary>
public sealed record ResolveManuallyDecision(PlayerId Player, int ChainItem, string CardId, string Text) : PendingDecision([Player]);

public enum TurnPoint { StartOfBeginning, StartOfMain, EndOfTurn }

/// <summary>Cards in play have an effect at this point of the turn: apply them with manual actions, then submit ContinueTurn.</summary>
public sealed record TurnPointDecision(PlayerId Player, TurnPoint Point, IReadOnlyList<ObjectId> Cards) : PendingDecision([Player]);
