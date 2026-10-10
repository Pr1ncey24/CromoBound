using CromoBound.Engine.Decisions;
using CromoBound.Engine.Events;
using CromoBound.Engine.Matches;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Views;

/// <summary>A card the viewer is allowed to see. Might is set for units only. <see cref="Effects"/> says whether the engine runs the card;
/// <see cref="ManualLines"/> are the text lines players resolve by hand (1-based).</summary>
public sealed record CardView(
    ObjectId Id, string CardId, string? PrintingId, PlayerId Owner, PlayerId Controller,
    bool Exhausted, bool Stunned, bool Buffed, bool Empowered, int Damage, int? Might, CombatRole? Role,
    MappingStatus Effects, IReadOnlyList<int> ManualLines, ObjectId? AttachedTo, int EmpowerCount = 0);

public sealed record PoolView(int Energy, IReadOnlyDictionary<Domain, int> Power, int UniversalPower);

/// <summary>One player's side. <see cref="Hand"/> and <see cref="Sideboard"/> are null in the opponent's view.</summary>
public sealed record PlayerSideView(
    PlayerId Player, int Points, int Xp, int GameWins, PoolView Pool,
    IReadOnlyList<CardView> Legend, IReadOnlyList<CardView> ChampionZone, IReadOnlyList<CardView> Base,
    IReadOnlyList<CardView>? Hand, int HandCount, int MainDeckCount, int RuneDeckCount,
    IReadOnlyList<CardView> Trash, IReadOnlyList<CardView> Banishment,
    IReadOnlyList<DeckEntry>? Sideboard, int SideboardCount);

/// <summary><see cref="Facedown"/> is set only for the facedown card's controller; others see <see cref="HasFacedown"/>.</summary>
public sealed record BattlefieldView(
    int Index, CardView Card, PlayerId? Controller, PlayerId? ContestedBy, IReadOnlyList<CardView> Units, bool HasFacedown, CardView? Facedown);

public sealed record ChainItemView(
    int Id, ChainItemKind Kind, PlayerId Controller, ChainItemStatus Status, CardView? Card, string? SourceCardId, string? Text,
    Place? Location, bool Accelerate, IReadOnlyList<IReadOnlyList<ObjectId>> Targets);

/// <summary>Turn state; <see cref="Scored"/> lists, per player index, the battlefields scored this turn.</summary>
public sealed record TurnView(
    int Number, PlayerId TurnPlayer, Phase Phase, TurnStep Step, PlayerId? Priority, PlayerId? Focus, bool Closed,
    int? ShowdownAt, bool Combat, PlayerId? Attacker, PlayerId? Defender, IReadOnlyList<IReadOnlyList<int>> Scored);

/// <summary>Everything one player may see (spec §10). <see cref="Decision"/> is set only when the viewer is deciding.</summary>
public sealed record PlayerView(
    PlayerId Viewer, MatchFormat Format, MatchStage Stage, int GameNumber, PlayerId? MatchWinner,
    IReadOnlyList<PlayerSideView> Players, TurnView? Turn, IReadOnlyList<BattlefieldView> Battlefields, IReadOnlyList<ChainItemView> Chain,
    IReadOnlyList<PlayerId> Deciding, string? DecisionKind, PendingDecision? Decision, IReadOnlyList<GameEvent> Log);
