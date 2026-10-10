using CromoBound.Client.Services;
using CromoBound.Contracts;
using CromoBound.Models.Cards;

namespace CromoBound.Client.Tests;

public class DeckTextTests
{
    /// <summary>A deck list as deck sites export it (this one is a real Fiora list).</summary>
    public const string Fiora = """
        Legend:
        1 Fiora, Grand Duelist

        Champion:
        1 Fiora, Victorious

        MainDeck:
        3 Dazzling Aurora
        2 Doran's Blade
        1 Repulse

        Battlefields:
        1 Amateur Recital
        1 Risen Altar
        1 Sunken Temple

        Rune Pool:
        8 Body Rune
        4 Order Rune

        Sideboard:
        2 Akshan, Mischievous
        1 Repulse
        """;

    private static CatalogCard Card(string id, string name, CardType type, string? defaultPrinting = null, Supertype? supertype = null) =>
        new(id, name, type, supertype, [], null, [], null, defaultPrinting);

    /// <summary>Each card has a printing "p-{id}" and is its default, except Doran's Blade: no default, two printings.</summary>
    public static CardCatalog Catalog { get; } = new(
        [
            Card("fiora-legend", "Fiora, Grand Duelist", CardType.Legend, "p-fiora-legend"),
            Card("fiora-champion", "Fiora, Victorious", CardType.Unit, "p-fiora-champion", Supertype.Champion),
            Card("dazzling-aurora", "Dazzling Aurora", CardType.Gear, "p-dazzling-aurora"),
            Card("dorans-blade", "Doran's Blade", CardType.Gear),
            Card("repulse", "Repulse", CardType.Spell, "p-repulse"),
            Card("amateur-recital", "Amateur Recital", CardType.Battlefield, "p-amateur-recital"),
            Card("risen-altar", "Risen Altar", CardType.Battlefield, "p-risen-altar"),
            Card("sunken-temple", "Sunken Temple", CardType.Battlefield, "p-sunken-temple"),
            Card("body-rune", "Body Rune", CardType.Rune, "p-body-rune", Supertype.Basic),
            Card("order-rune", "Order Rune", CardType.Rune, "p-order-rune", Supertype.Basic),
            Card("akshan", "Akshan, Mischievous", CardType.Unit, "p-akshan", Supertype.Champion),
            Card("token-bird", "Repulse Bird", CardType.Unit, null, Supertype.Token),
            Card("rift-herald", "Rift Herald", CardType.Unit, "p-rift-herald-2"),
        ],
        [
            .. new[] { "fiora-legend", "fiora-champion", "dazzling-aurora", "repulse", "amateur-recital", "risen-altar", "sunken-temple",
                "body-rune", "order-rune", "akshan" }.Select(id => new CatalogPrinting($"p-{id}", id, Orientation.Portrait)),
            new CatalogPrinting("p-dorans-blade-2", "dorans-blade", Orientation.Portrait),
            new CatalogPrinting("p-dorans-blade-1", "dorans-blade", Orientation.Portrait),
            new CatalogPrinting("p-rift-herald-1", "rift-herald", Orientation.Portrait),
            new CatalogPrinting("p-rift-herald-2", "rift-herald", Orientation.Portrait),
        ]);

    private static Deck Parsed(string text)
    {
        var (deck, error) = DeckText.Parse(text, Catalog);
        Assert.Null(error);
        return deck!;
    }

    private static string Refused(string text)
    {
        var (deck, error) = DeckText.Parse(text, Catalog);
        Assert.Null(deck);
        return error!;
    }

    [Fact]
    public void A_deck_list_reads_every_section_with_default_printings()
    {
        var deck = Parsed(Fiora);

        Assert.Equal("Fiora, Grand Duelist", deck.Name);
        Assert.Equal(("p-fiora-legend", "p-fiora-champion"), (deck.Legend, deck.Champion));
        Assert.Equal(new[] { ("p-dazzling-aurora", 3), ("p-dorans-blade-1", 2), ("p-repulse", 1) }, deck.Main.Select(e => (e.Printing, e.Count)));
        Assert.Equal(new[] { "p-amateur-recital", "p-risen-altar", "p-sunken-temple" }, deck.Battlefields);
        Assert.Equal(new[] { ("p-body-rune", 8), ("p-order-rune", 4) }, deck.Runes.Select(e => (e.Printing, e.Count)));
        Assert.Equal(new[] { ("p-akshan", 2), ("p-repulse", 1) }, deck.Sideboard.Select(e => (e.Printing, e.Count)));
    }

    [Fact]
    public void A_cards_default_printing_wins_over_its_first_one()
    {
        var deck = Parsed("""
            Legend:
            1 Fiora, Grand Duelist
            Champion:
            1 Fiora, Victorious
            MainDeck:
            3 Rift Herald
            """);

        Assert.Equal(("p-rift-herald-2", 3), (deck.Main[0].Printing, deck.Main[0].Count));
    }

    [Fact]
    public void Names_match_in_any_case_with_curly_apostrophes_extra_spaces_and_an_x_after_the_count()
    {
        var deck = Parsed("""
            legend:
            1x  fiora,   grand duelist
            CHAMPION:
            1 Fiora, Victorious
            Main Deck:
            2x Doran’s Blade
            """);

        Assert.Equal("p-fiora-legend", deck.Legend);
        Assert.Equal(("p-dorans-blade-1", 2), (deck.Main[0].Printing, deck.Main[0].Count));
    }

    [Fact]
    public void Windows_line_endings_and_a_byte_order_mark_are_read()
    {
        var deck = Parsed("\uFEFFLegend:\r\n1 Fiora, Grand Duelist\r\nChampion:\r\n1 Fiora, Victorious\r\nRunes:\r\n6 Body Rune\r\n");

        Assert.Equal(("p-body-rune", 6), (deck.Runes[0].Printing, deck.Runes[0].Count));
    }

    [Fact]
    public void The_same_card_twice_in_a_section_is_added_up()
    {
        var deck = Parsed("Legend:\n1 Fiora, Grand Duelist\nChampion:\n1 Fiora, Victorious\nMainDeck:\n2 Repulse\n1 Repulse\n");

        Assert.Equal(("p-repulse", 3), (Assert.Single(deck.Main).Printing, deck.Main[0].Count));
    }

    [Fact]
    public void Unknown_cards_are_all_named()
    {
        var error = Refused(Fiora.Replace("Dazzling Aurora", "Dazling Aurora").Replace("Sunken Temple", "Sunken Tempel"));

        Assert.Equal("These cards aren't known: Dazling Aurora, Sunken Tempel.", error);
    }

    [Fact]
    public void Tokens_are_not_cards_a_deck_can_hold()
    {
        Assert.Equal("These cards aren't known: Repulse Bird.", Refused(Fiora + "\n1 Repulse Bird\n"));
    }

    [Theory]
    [InlineData("Champion:\n1 Fiora, Victorious\n", DeckText.NoLegend)]
    [InlineData("Legend:\n1 Fiora, Grand Duelist\n", DeckText.NoChampion)]
    [InlineData("Legend:\n1 Fiora, Grand Duelist\n1 Fiora, Grand Duelist\nChampion:\n1 Fiora, Victorious\n", "The Legend section has more than one card.")]
    [InlineData("1 Repulse\nLegend:\n1 Fiora, Grand Duelist\n", "Line 1 comes before any section, like Legend: or MainDeck:.")]
    [InlineData("Legend:\n1 Fiora, Grand Duelist\nExtras:\n1 Repulse\n", "Line 3 isn't a section this app knows: Extras:")]
    [InlineData("Legend:\nFiora, Grand Duelist\n", "Line 2 isn't a card line (a count, then the card's name): Fiora, Grand Duelist")]
    [InlineData("Legend:\n0 Fiora, Grand Duelist\n", "Line 2 isn't a card line (a count, then the card's name): 0 Fiora, Grand Duelist")]
    public void A_list_that_cant_be_read_says_why(string text, string error)
    {
        Assert.Equal(error, Refused(text));
    }

    [Theory]
    [InlineData("{ \"name\": \"x\" }", false)]
    [InlineData("  \n {", false)]
    [InlineData("\uFEFF{", false)]
    [InlineData("[1, 2, 3]", false)]
    [InlineData("Legend:\n1 Fiora, Grand Duelist", true)]
    public void Only_text_that_doesnt_start_with_a_brace_is_a_deck_list(string input, bool isList)
    {
        Assert.Equal(isList, DeckText.LooksLikeList(input));
    }
}
