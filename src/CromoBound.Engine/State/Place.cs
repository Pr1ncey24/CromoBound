namespace CromoBound.Engine.State;

public enum PlaceKind { Hand, MainDeck, RuneDeck, Trash, Banishment, ChampionZone, LegendZone, Base, Battlefield, Facedown, BattlefieldCard, Chain }

/// <summary>
/// Where an object is. Player piles and Base carry <see cref="Player"/>; battlefield places carry <see cref="Index"/>.
/// <see cref="PlaceKind.Battlefield"/> is the location units stand at; <see cref="PlaceKind.BattlefieldCard"/> holds the battlefield card itself.
/// </summary>
public readonly record struct Place(PlaceKind Kind, PlayerId? Player, int? Index)
{
    public static Place Hand(PlayerId player) => new(PlaceKind.Hand, player, null);
    public static Place MainDeck(PlayerId player) => new(PlaceKind.MainDeck, player, null);
    public static Place RuneDeck(PlayerId player) => new(PlaceKind.RuneDeck, player, null);
    public static Place Trash(PlayerId player) => new(PlaceKind.Trash, player, null);
    public static Place Banishment(PlayerId player) => new(PlaceKind.Banishment, player, null);
    public static Place ChampionZone(PlayerId player) => new(PlaceKind.ChampionZone, player, null);
    public static Place LegendZone(PlayerId player) => new(PlaceKind.LegendZone, player, null);
    public static Place Base(PlayerId player) => new(PlaceKind.Base, player, null);
    public static Place Battlefield(int index) => new(PlaceKind.Battlefield, null, index);
    public static Place Facedown(int index) => new(PlaceKind.Facedown, null, index);
    public static Place BattlefieldCard(int index) => new(PlaceKind.BattlefieldCard, null, index);
    public static Place Chain { get; } = new(PlaceKind.Chain, null, null);

    /// <summary>Board zones (CR 107). Moving into or out of anything else makes a new object.</summary>
    public bool IsBoard => Kind is PlaceKind.LegendZone or PlaceKind.Base or PlaceKind.Battlefield or PlaceKind.Facedown or PlaceKind.BattlefieldCard;

    /// <summary>Places a unit or gear can be: a Base or a battlefield.</summary>
    public bool IsLocation => Kind is PlaceKind.Base or PlaceKind.Battlefield;

    public bool IsOrdered => Kind is PlaceKind.MainDeck or PlaceKind.RuneDeck or PlaceKind.Chain;

    /// <summary>Non-board zones that belong to one player; cards always go to their owner's (CR 056).</summary>
    public bool IsPlayerPile => Kind is PlaceKind.Hand or PlaceKind.MainDeck or PlaceKind.RuneDeck or PlaceKind.Trash or PlaceKind.Banishment or PlaceKind.ChampionZone;

    public override string ToString() => Player is { } p ? $"{Kind}({p})" : Index is { } i ? $"{Kind}({i})" : Kind.ToString();
}
