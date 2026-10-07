using CromoBound.Importer;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Models.Tests;

public class ScaffolderTests
{
    private static Card Card(string id, string rich) => new() { Id = id, Name = id, Type = CardType.Unit, Text = new CardText { Rich = rich } };

    [Fact]
    public void Vanilla_card_is_full_with_no_abilities()
    {
        var file = Scaffolder.Classify(Card("vanguard-sergeant", ""))!;

        Assert.Equal(MappingStatus.Full, file.Status);
        Assert.Equal(Scaffolder.SchemaRef, file.Schema);
        Assert.Empty(file.Keywords);
        Assert.Empty(file.Abilities);
    }

    [Fact]
    public void Keyword_only_card_is_full_with_keywords()
    {
        var file = Scaffolder.Classify(Card("mystic-poro", "<p>[Vision] (When you play me, look at the top card.)</p>"))!;
        Assert.Equal(MechanicalKeyword.Vision, Assert.Single(file.Keywords).Keyword);
    }

    [Fact]
    public void Other_cards_are_not_scaffolded() =>
        Assert.Null(Scaffolder.Classify(Card("vengeance", "<p>Kill a unit.</p>")));

    [Fact]
    public void Multi_domain_keyword_only_card_is_scaffolded() =>
        Assert.NotNull(Scaffolder.Classify(Card("odd-signature", "<p>[Tank]</p>") with
        {
            Domains = [Domain.Calm, Domain.Body],
            Cost = new CardCost { Energy = 3, Power = [PowerSymbol.Self] },
        }));

    [Fact]
    public void Scaffolder_never_overwrites_existing_files()
    {
        using var temp = new TempDir();
        var effectsDir = Path.Combine(temp.Root, "effects");
        const string handWritten = """{ "cardId": "vanguard-sergeant", "status": "Partial" }""";
        var existing = temp.Write("effects/vanguard-sergeant.json", handWritten);
        var report = new ImportReport();

        Scaffolder.Run([Card("vanguard-sergeant", ""), Card("mystic-poro", "<p>[Vision]</p>"), Card("vengeance", "<p>Kill a unit.</p>")], effectsDir, report);

        Assert.Equal(handWritten, File.ReadAllText(existing));
        Assert.True(File.Exists(Path.Combine(effectsDir, "mystic-poro.json")));
        Assert.False(File.Exists(Path.Combine(effectsDir, "vengeance.json")));
        Assert.Equal(1, report.StatusCounts[MappingStatus.Partial]);
        Assert.Equal(1, report.StatusCounts[MappingStatus.Full]);
        Assert.Equal(1, report.StatusCounts[MappingStatus.Unmapped]);
    }

    [Fact]
    public void Report_lists_every_section()
    {
        var report = new ImportReport();
        report.TypeCounts[CardType.Unit] = 2;
        report.StatusCounts[MappingStatus.Full] = 1;
        report.MissingTokens.Add("token-mech");
        report.UnknownKeywords["Bogus"] = "odd-knight";
        report.TextConflicts.Add(new TextConflict("ahri-inquisitive", ConflictKind.Wording, ["<p>Old</p>", "<p>New</p>"]));

        var markdown = ReportWriter.ToMarkdown(report);

        Assert.Contains("| Unit | 2 |", markdown);
        Assert.Contains("| Full | 1 |", markdown);
        Assert.Contains("`token-mech`", markdown);
        Assert.Contains("`Bogus` (first seen on `odd-knight`)", markdown);
        Assert.Contains("`ahri-inquisitive` (Wording)", markdown);
        Assert.DoesNotContain("\r", markdown);
    }

    [Fact]
    public void Normalize_command_writes_generated_files_effects_and_report()
    {
        using var temp = new TempDir();
        temp.Write("raw/cards.json", """
            [ { "id": "p1", "name": "Vanguard Sergeant", "attributes": { "energy": 4, "might": 4, "power": null },
                "classification": { "type": "Unit", "supertype": null, "rarity": "Common", "domain": ["Order"] },
                "text": { "rich": "", "plain": "" }, "set": { "set_id": "OGN", "label": "Origins" },
                "media": { "image_url": "https://img.test/p1.png", "artist": "A" }, "tags": ["Demacia"], "orientation": "portrait",
                "metadata": { "alternate_art": false, "overnumbered": false, "signature": false } } ]
            """);
        temp.Write("raw/sets.json", """[ { "set_id": "OGN", "name": "Origins", "card_count": 1, "published_on": "2025-10-31T00:00:00" } ]""");
        temp.Write("tokens.json", "[]");

        var report = ImportCommands.Normalize(temp.Root);

        Assert.Equal(1, report.TypeCounts[CardType.Unit]);
        Assert.Contains("\"id\": \"vanguard-sergeant\"", File.ReadAllText(Path.Combine(temp.Root, "cards.json")));
        Assert.Contains("\"cardId\": \"vanguard-sergeant\"", File.ReadAllText(Path.Combine(temp.Root, "printings.json")));
        Assert.True(File.Exists(Path.Combine(temp.Root, "sets.json")));
        Assert.True(File.Exists(Path.Combine(temp.Root, "effects", "vanguard-sergeant.json")));
        Assert.Contains("# Import report", File.ReadAllText(Path.Combine(temp.Root, "import-report.md")));
    }
}
