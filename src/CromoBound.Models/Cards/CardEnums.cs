using System.Text.Json.Serialization;

namespace CromoBound.Models.Cards;

public enum CardType { Unit, Spell, Gear, Rune, Battlefield, Legend }

public enum Supertype { Champion, Signature, Basic, Token }

public enum Domain { Fury, Calm, Mind, Body, Chaos, Order, Colorless }

public enum Rarity { Common, Uncommon, Rare, Epic, Showcase, Promo }

public enum PrintingVariant { Standard, AlternateArt, Overnumbered, Signature, Metal, Starter, Ultimate, LaunchExclusive, Other }

public enum Orientation { Portrait, Landscape }

/// <summary>Bracketed card-text terms used for search and filters. Not used by the rules.</summary>
public enum DisplayKeyword
{
    Accelerate, Action, Ambush, Assault, Backline, Buff, Burn, Deathknell, Deflect, Empower, Empowered,
    Equip, Flow, Ganking, Hidden, Hunt, Legion, Level, Mighty,
    [JsonStringEnumMemberName("Quick-Draw")] QuickDraw,
    Reaction, Repeat, Shield, Stun, Tank, Temporary, Unique, Vision, Weaponmaster,
}
