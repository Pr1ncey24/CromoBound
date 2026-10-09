using CromoBound.Engine.Tests;
using CromoBound.Models.Cards;

namespace CromoBound.Server.Tests;

/// <summary>Decks over <see cref="ServerFactory.TestCards"/>: two legal ones with different battlefields, and one with a single battlefield.</summary>
internal static class Decks
{
    public static Deck First => TestDecks.Jinx("bf-a", "bf-b", "bf-c");
    public static Deck Second => TestDecks.Jinx("bf-d", "bf-e", "bf-f");
    public static Deck Illegal => TestDecks.Jinx("bf-a");
}
