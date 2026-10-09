using CromoBound.Engine.Rules;
using CromoBound.Engine.State;
using CromoBound.Models.Effects;

namespace CromoBound.Engine.Effects.Resolvers;

/// <summary>Turns zone references into places (spec §4.5): one per owner, in turn order. A missing owner means "You".</summary>
internal static class ZoneResolver
{
    public static List<Place> Resolve(Game game, EffectContext context, ZoneRef zone) =>
        [.. PlayerResolver.Resolve(game, context, zone.Owner).Select(player => PlaceOf(zone.Zone, player))];

    private static Place PlaceOf(Zone zone, PlayerId player) => zone switch
    {
        Zone.Trash => Place.Trash(player),
        Zone.Hand => Place.Hand(player),
        Zone.MainDeck => Place.MainDeck(player),
        Zone.RuneDeck => Place.RuneDeck(player),
        Zone.Base => Place.Base(player),
        Zone.Banishment => Place.Banishment(player),
        Zone.ChampionZone => Place.ChampionZone(player),
        _ => throw new InvalidOperationException($"The {zone} zone has no single place per player."),
    };
}
