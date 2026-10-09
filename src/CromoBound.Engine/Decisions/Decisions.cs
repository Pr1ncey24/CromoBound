using System.Text.Json.Serialization;
using CromoBound.Engine.Actions;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Engine.Decisions;

/// <summary>What the engine is waiting for and from whom.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PriorityDecision), "Priority")]
[JsonDerivedType(typeof(PlayChoicesDecision), "PlayChoices")]
[JsonDerivedType(typeof(PayCostDecision), "PayCost")]
[JsonDerivedType(typeof(ChooseShowdownDecision), "ChooseShowdown")]
[JsonDerivedType(typeof(AssignDamageDecision), "AssignDamage")]
[JsonDerivedType(typeof(ResolveManuallyDecision), "ResolveManually")]
[JsonDerivedType(typeof(TurnPointDecision), "TurnPoint")]
[JsonDerivedType(typeof(PickBattlefieldDecision), "PickBattlefield")]
[JsonDerivedType(typeof(ChoosePlayOrderDecision), "ChoosePlayOrder")]
[JsonDerivedType(typeof(SideboardDecision), "Sideboard")]
[JsonDerivedType(typeof(MulliganDecision), "Mulligan")]
[JsonDerivedType(typeof(ConfirmUndoDecision), "ConfirmUndo")]
[JsonDerivedType(typeof(ChooseTargetsDecision), "ChooseTargets")]
[JsonDerivedType(typeof(ChoosePlayerDecision), "ChoosePlayer")]
[JsonDerivedType(typeof(ChooseCardsDecision), "ChooseCards")]
[JsonDerivedType(typeof(OptionalDecision), "Optional")]
[JsonDerivedType(typeof(OrderTriggersDecision), "OrderTriggers")]
public abstract record PendingDecision(IReadOnlyList<PlayerId> Players);

public sealed record RuneOption(ObjectId Rune, bool CanExhaust);

public sealed record MoveOption(ObjectId Unit, IReadOnlyList<Place> Destinations);

public sealed record HideOption(ObjectId Card, IReadOnlyList<int> Battlefields);

/// <summary>An activated ability the player may start now: its source and the ability's index in its card's abilities.</summary>
public sealed record ActivateOption(ObjectId Source, int Ability);

/// <summary>The priority (or focus) holder's options. Playable lists cards by timing only; payment is checked when paying.</summary>
public sealed record PriorityDecision(
    PlayerId Player,
    [property: KeepEmpty] IReadOnlyList<ObjectId> Playable,
    [property: KeepEmpty] IReadOnlyList<RuneOption> Runes,
    [property: KeepEmpty] IReadOnlyList<MoveOption> Moves,
    [property: KeepEmpty] IReadOnlyList<HideOption> Hides,
    [property: KeepEmpty] IReadOnlyList<ActivateOption> Activations,
    bool CanPass,
    bool CanEndTurn) : PendingDecision([Player]);

public sealed record PlayChoicesDecision(PlayerId Player, ObjectId Card, IReadOnlyList<Place> Locations, bool AccelerateAvailable)
    : PendingDecision([Player]);

/// <summary>Energy plus one power symbol per entry. Power is written even when empty, so it never reads back as null.</summary>
public sealed record TotalCost(int Energy, [property: KeepEmpty] IReadOnlyList<PowerSymbol> Power);

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

/// <summary>Choose between <see cref="Min"/> and <see cref="Max"/> of <see cref="Options"/> as the targets of slot <see cref="Slot"/>
/// of the card being played (spec §5.1).</summary>
public sealed record ChooseTargetsDecision(PlayerId Player, ObjectId Card, int Slot, IReadOnlyList<ObjectId> Options, int Min, int Max)
    : PendingDecision([Player]);

/// <summary>Choose one of <see cref="Options"/> for an effect of <see cref="CardId"/> (spec §6.1).</summary>
public sealed record ChoosePlayerDecision(PlayerId Player, string CardId, IReadOnlyList<PlayerId> Options) : PendingDecision([Player]);

/// <summary>Choose between <see cref="Min"/> and <see cref="Max"/> of <see cref="Options"/> for an effect or a cost of <see cref="CardId"/>.</summary>
public sealed record ChooseCardsDecision(PlayerId Player, string CardId, IReadOnlyList<ObjectId> Options, int Min, int Max)
    : PendingDecision([Player]);

/// <summary>Yes or no to an optional part of an effect of <see cref="CardId"/>; <see cref="Text"/> says what.</summary>
public sealed record OptionalDecision(PlayerId Player, string CardId, string Text) : PendingDecision([Player]);

/// <summary>A triggered ability waiting to go on the chain: its source (which may have left play), the source's card id and the ability's text, if known.</summary>
public sealed record TriggerOption(ObjectId Source, string CardId, string? Text);

/// <summary>Several triggered abilities of the player wait at once: choose the order they go on the chain (spec §5.2).</summary>
public sealed record OrderTriggersDecision(PlayerId Player, IReadOnlyList<TriggerOption> Triggers) : PendingDecision([Player]);
