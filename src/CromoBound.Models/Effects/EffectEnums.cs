namespace CromoBound.Models.Effects;

public enum MappingStatus { Full, Partial, Unmapped }

public enum Timing { Action, Reaction }

public enum LimitPeriod { Turn, Combat, Game }

public enum TriggerEvent
{
    Played, Discarded, Drawn, Recycled, Chosen,
    Attack, Defend, Conquer, Hold, Scored,
    Moved, Dies, Killed,
    Stunned, Buffed, Readied, Equipped, BecameEmpowered, BecameMighty, Damaged,
    PhaseStart, TurnEnd, CombatStart, CombatEnd,
}

public enum PlayCostMode { IgnoreAll, IgnoreEnergy, IgnorePower }

/// <summary>A domain, "Any" ([A], rainbow) or "Self" ([C], this card's domain).</summary>
public enum PowerSymbol { Fury, Calm, Mind, Body, Chaos, Order, Any, Self }

public enum Duration { ThisTurn, ThisCombat, UntilLeavesBoard, WhileInZone, Permanent }

public enum Permission { PlayToBattlefieldWithEnemyUnits }

public enum UntargetableBy { EnemySpellsAndAbilities }
