using CromoBound.Importer;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Models.Tests;

public class ImporterTextTests
{
    [Theory]
    [InlineData("Falling Star", "falling-star")]
    [InlineData("Rengar, Trophy Hunter", "rengar-trophy-hunter")]
    [InlineData("Kai'Sa, Survivor", "kaisa-survivor")]
    [InlineData("Super Mega Death Rocket!", "super-mega-death-rocket")]
    [InlineData("Dr. Mundo, Expert", "dr-mundo-expert")]
    [InlineData("Pokémon  Café", "pokemon-cafe")]
    public void Slug_is_lowercase_ascii_with_single_dashes(string name, string expected) =>
        Assert.Equal(expected, Slug.From(name));

    [Theory]
    [InlineData("Ahri - Inquisitive", "Ahri, Inquisitive", null)]
    [InlineData("Ahri - Inquisitive (Signature)", "Ahri, Inquisitive", "Signature")]
    [InlineData("Ahri, Inquisitive (Alternate Art)", "Ahri, Inquisitive", "Alternate Art")]
    [InlineData("Recruit (273) // Buff", "Recruit", "273")]
    [InlineData("Gold // Buff", "Gold", null)]
    [InlineData("Kharox", "Kharox", null)]
    public void NameNormalizer_unifies_variants(string raw, string name, string? suffix)
    {
        var result = NameNormalizer.Normalize(raw);
        Assert.Equal(name, result.Name);
        Assert.Equal(suffix, result.Suffix);
    }

    [Fact]
    public void Variant_names_share_one_slug() =>
        Assert.Equal(
            Slug.From(NameNormalizer.Normalize("Ahri - Inquisitive (Overnumbered)").Name),
            Slug.From(NameNormalizer.Normalize("Ahri, Inquisitive").Name));

    [Fact]
    public void ExtractDisplay_finds_keywords_and_reports_unknown_terms()
    {
        var unknown = new List<string>();

        var keywords = KeywordText.ExtractDisplay("<p>[Burn 3]. [Quick-Draw] [Add] [Level 6][&gt;] [Bogus] [Burn 1]</p>", unknown);

        Assert.Equal(new[] { DisplayKeyword.Burn, DisplayKeyword.QuickDraw, DisplayKeyword.Level }, keywords);
        Assert.Equal(new[] { "Bogus" }, unknown);
    }

    [Fact]
    public void ExtractDisplay_finds_predict_with_and_without_amount()
    {
        var unknown = new List<string>();

        var keywords = KeywordText.ExtractDisplay("<p>[Predict]. [Predict 2]</p>", unknown);

        Assert.Equal(new[] { DisplayKeyword.Predict }, keywords);
        Assert.Empty(unknown);
    }

    [Fact]
    public void KeywordOnly_parses_single_keyword_with_reminder()
    {
        var entries = KeywordText.TryParseKeywordOnly("<p>[Vision] (When you play me, look at the top card of your Main Deck.)</p>");
        Assert.Equal(MechanicalKeyword.Vision, Assert.Single(entries!).Keyword);
    }

    [Fact]
    public void KeywordOnly_parses_values_across_lines()
    {
        var entries = KeywordText.TryParseKeywordOnly("<p>[Assault 2]<br />[Tank][Shield]</p>")!;

        Assert.Equal(3, entries.Count);
        Assert.Equal(MechanicalKeyword.Assault, entries[0].Keyword);
        Assert.Equal(2, entries[0].Value);
        Assert.Equal(MechanicalKeyword.Tank, entries[1].Keyword);
        Assert.Null(entries[2].Value);
    }

    [Theory]
    [InlineData("<p>[Deathknell] — Channel 1 rune exhausted.</p>")]
    [InlineData("<p>[Vision]<br />Other friendly units have [Vision].</p>")]
    [InlineData("<p>[Legion] — I cost :rb_energy_2: less.</p>")]
    [InlineData("<p>[Repeat] :rb_energy_2:</p>")]
    [InlineData("<p>[Tank 2]</p>")]
    [InlineData("<p>[NO TEXT]</p>")]
    [InlineData("")]
    public void KeywordOnly_rejects_anything_else(string rich) => Assert.Null(KeywordText.TryParseKeywordOnly(rich));

    [Theory]
    [InlineData("Quick-Draw", true)]
    [InlineData("ADD", true)]
    [InlineData("11", true)]
    [InlineData("TEXT", true)]
    [InlineData("Stun", true)]
    [InlineData("Overcharge", false)]
    public void Index_keywords_are_known_or_noise(string value, bool known) =>
        Assert.Equal(known, KeywordText.IsKnownIndexKeyword(value));
}
