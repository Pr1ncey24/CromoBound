using CromoBound.Importer;
using CromoBound.Models.Cards;

namespace CromoBound.Models.Tests;

public class NormalizerTests
{
    private static readonly Card RecruitToken = new()
    {
        Id = "token-recruit", Name = "Recruit", Type = CardType.Unit, Supertype = Supertype.Token, Might = 1, Text = new CardText(),
    };

    private static (NormalizeResult Result, ImportReport Report) Run(params RawCard[] cards)
    {
        var report = new ImportReport();
        return (Normalizer.Normalize(cards, RawFixtures.Sets, [RecruitToken], report), report);
    }

    [Fact]
    public void Groups_name_variants_into_one_card_with_newest_text()
    {
        var (result, report) = Run(
            RawFixtures.Card("old", "Ahri - Inquisitive", set: "OGN", rich: "<p>Old wording.</p>"),
            RawFixtures.Card("new", "Ahri, Inquisitive (Alternate Art)", set: "VEN", rich: "<p>New wording.</p>", altArt: true));

        var card = Assert.Single(result.Cards);
        Assert.Equal("ahri-inquisitive", card.Id);
        Assert.Equal("Ahri, Inquisitive", card.Name);
        Assert.Equal("<p>New wording.</p>", card.Text.Rich);
        Assert.Equal("old", card.DefaultPrintingId);
        Assert.Equal(2, result.Printings.Count);
        Assert.Equal(PrintingVariant.AlternateArt, result.Printings.Single(p => p.Id == "new").Variant);
        Assert.Equal(PrintingVariant.Standard, result.Printings.Single(p => p.Id == "old").Variant);
        Assert.Equal(ConflictKind.Wording, Assert.Single(report.TextConflicts).Kind);
    }

    [Fact]
    public void Reminder_only_difference_is_reported_as_reminder_only()
    {
        var (_, report) = Run(
            RawFixtures.Card("a", "Mystic Poro", rich: "<p>[Vision] (Look at the top card.)</p>"),
            RawFixtures.Card("b", "Mystic Poro (Metal)", set: "VEN", rich: "<p>[Vision]</p>"));

        Assert.Equal(ConflictKind.ReminderOnly, Assert.Single(report.TextConflicts).Kind);
    }

    [Fact]
    public void Power_is_derived_for_single_domain_cards()
    {
        var (result, _) = Run(RawFixtures.Card("a", "Falling Star", type: "Spell", energy: 2, might: null, power: 2));
        Assert.Equal(new[] { Domain.Fury, Domain.Fury }, result.Cards[0].Cost!.Power!);
    }

    [Fact]
    public void Power_is_unknown_for_multi_domain_cards()
    {
        var (result, report) = Run(RawFixtures.Card("a", "Showstopper", type: "Spell", domains: ["Body", "Order"], energy: 1, might: null, power: 1));

        Assert.Null(result.Cards[0].Cost!.Power);
        Assert.Contains("showstopper", report.MissingPowerDomains);
    }

    [Fact]
    public void Energy_only_cards_have_empty_power_and_costless_cards_have_no_cost()
    {
        var (result, _) = Run(
            RawFixtures.Card("a", "Vanguard Sergeant", energy: 4),
            RawFixtures.Card("b", "Fury Rune", type: "Rune", supertype: "Basic", energy: null, might: null));

        Assert.Empty(result.Cards.Single(c => c.Id == "vanguard-sergeant").Cost!.Power!);
        Assert.Null(result.Cards.Single(c => c.Id == "fury-rune").Cost);
    }

    [Fact]
    public void Token_prints_become_printings_of_token_cards()
    {
        var (result, report) = Run(
            RawFixtures.Card("t1", "Recruit (273) // Buff", supertype: "Token", energy: null, might: 1),
            RawFixtures.Card("t2", "Mech // Buff", supertype: "Token", energy: null, might: 3));

        Assert.Empty(result.Cards);
        Assert.Equal("token-recruit", result.Printings.Single(p => p.Id == "t1").CardId);
        Assert.Equal(new[] { "token-mech" }, report.MissingTokens);
    }

    [Fact]
    public void Unknown_type_fails_with_card_name()
    {
        var error = Assert.Throws<ImportException>(() => Run(RawFixtures.Card("a", "Strange Thing", type: "Planeswalker")));
        Assert.Contains("Strange Thing", error.Message);
        Assert.Contains("Planeswalker", error.Message);
    }

    [Fact]
    public void Unknown_rarity_fails_with_card_name()
    {
        var error = Assert.Throws<ImportException>(() => Run(RawFixtures.Card("a", "Shiny Thing", rarity: "Mythic")));
        Assert.Contains("Shiny Thing", error.Message);
        Assert.Contains("Mythic", error.Message);
    }

    [Fact]
    public void Display_keywords_are_extracted_and_unknown_ones_reported()
    {
        var (result, report) = Run(RawFixtures.Card("a", "Odd Knight", rich: "<p>[Shield 2]<br />[Bogus]</p>"));

        Assert.Equal(new[] { DisplayKeyword.Shield }, result.Cards[0].Keywords);
        Assert.Equal("odd-knight", report.UnknownKeywords["Bogus"]);
    }

    [Fact]
    public void Slug_collision_fails()
    {
        var error = Assert.Throws<ImportException>(() => Run(RawFixtures.Card("a", "Kai'Sa"), RawFixtures.Card("b", "KaiSa")));
        Assert.Contains("kaisa", error.Message);
    }

    [Fact]
    public void Title_only_legend_merges_into_full_name_legend()
    {
        var (result, _) = Run(
            RawFixtures.Card("full", "Ambessa - Matriarch of War", set: "VEN", type: "Legend", energy: null, might: null),
            RawFixtures.Card("short", "Matriarch of War", set: "VEN", type: "Legend", energy: null, might: null, number: 2));

        var card = Assert.Single(result.Cards);
        Assert.Equal("ambessa-matriarch-of-war", card.Id);
        Assert.Equal("Ambessa, Matriarch of War", card.Name);
        Assert.All(result.Printings, p => Assert.Equal("ambessa-matriarch-of-war", p.CardId));
    }

    [Fact]
    public void Title_only_non_legend_is_not_merged()
    {
        var (result, _) = Run(
            RawFixtures.Card("full", "Ambessa - Matriarch of War", type: "Legend", energy: null, might: null),
            RawFixtures.Card("unit", "Matriarch of War"));

        Assert.Equal(new[] { "ambessa-matriarch-of-war", "matriarch-of-war" }, result.Cards.Select(c => c.Id));
    }

    [Fact]
    public void Title_only_legend_matching_several_full_names_fails()
    {
        var error = Assert.Throws<ImportException>(() => Run(
            RawFixtures.Card("a", "Ambessa - Hero", type: "Legend", energy: null, might: null),
            RawFixtures.Card("b", "Akali - Hero", type: "Legend", energy: null, might: null),
            RawFixtures.Card("c", "Hero", type: "Legend", energy: null, might: null)));
        Assert.Contains("'Hero'", error.Message);
    }

    [Fact]
    public void Sets_are_mapped_and_type_counts_recorded()
    {
        var (result, report) = Run(RawFixtures.Card("a", "Vanguard Sergeant"));

        Assert.Equal(new DateOnly(2026, 7, 31), result.Sets.Single(s => s.Id == "VEN").PublishedOn);
        Assert.Equal(1, report.TypeCounts[CardType.Unit]);
    }
}
