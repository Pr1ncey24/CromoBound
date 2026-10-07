using System.Text.Json.Serialization;

namespace CromoBound.Models.Effects;

public enum RefKind { Self, Here, Controller, Owner, TriggerSubject, TriggerSource }

public enum SelectKind { Unit, Gear, Permanent, Spell, Card, Rune, Battlefield, Legend, ChainItem, Player }

/// <summary>Friendly/Enemy apply to objects; Self/Opponent/Ally apply to players.</summary>
public enum Relation { Friendly, Enemy, Self, Opponent, Ally }

public enum ObjectStatus { Stunned, Exhausted, Buffed, Damaged, Empowered }

public enum Zone { Board, Hand, MainDeck, RuneDeck, Trash, Banishment, ChampionZone, Base, Facedown, Legend, Chain }

public enum DeckPosition { Top, Bottom }

public enum PlayerKind { You, Opponent, EachPlayer, EachOpponent }

public enum ValueProperty { Might, EnergyCost, PowerCost, Damage }

public enum CompareOp { Eq, Lte, Gte, Lt, Gt }

public enum Phase { Awaken, Beginning, Channel, Draw, Main, Ending }

/// <summary>Keywords that are abilities an object has (Core Rules 800-829).</summary>
public enum MechanicalKeyword
{
    Accelerate, Action, Ambush, Assault, Backline, Deathknell, Deflect, Empower, Equip, Flow, Ganking, Hidden, Hunt,
    [JsonStringEnumMemberName("Quick-Draw")] QuickDraw,
    Reaction, Repeat, Shield, Tank, Temporary, Unique, Vision, Weaponmaster,
}
