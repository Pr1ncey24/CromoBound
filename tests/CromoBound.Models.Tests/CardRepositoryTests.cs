using CromoBound.Data;
using CromoBound.Models.Effects;

namespace CromoBound.Models.Tests;

public class CardRepositoryTests
{
    private const string Cards = """
        [ { "id": "kharox", "name": "Kharox", "type": "Unit", "text": { "rich": "<p>a<br />b</p>" } },
          { "id": "vanguard-sergeant", "name": "Vanguard Sergeant", "type": "Unit", "text": {} } ]
        """;
    private const string Tokens = """[ { "id": "token-recruit", "name": "Recruit", "type": "Unit", "supertype": "Token", "might": 1, "text": {} } ]""";
    private const string Printings = """[ { "id": "p1", "cardId": "kharox", "set": "VEN" } ]""";
    private const string Sets = """[ { "id": "VEN", "name": "Vendetta", "cardCount": 1 } ]""";

    private static TempDir CreateData()
    {
        var temp = new TempDir();
        temp.Write("cards.json", Cards);
        temp.Write("tokens.json", Tokens);
        temp.Write("printings.json", Printings);
        temp.Write("sets.json", Sets);
        temp.Write("effects/kharox.json", """{ "cardId": "kharox", "status": "Partial" }""");
        return temp;
    }

    [Fact]
    public void Loads_cards_tokens_printings_sets_and_effects()
    {
        using var temp = CreateData();

        var db = CardRepository.Load(temp.Root);

        Assert.Equal(3, db.Cards.Count);
        Assert.True(db.Cards.ContainsKey("token-recruit"));
        Assert.Equal("kharox", db.Printings["p1"].CardId);
        Assert.Equal("Vendetta", db.Sets["VEN"].Name);
        Assert.Equal(MappingStatus.Partial, db.StatusOf("kharox"));
        Assert.EndsWith("kharox.json", db.Effects["kharox"].FilePath);
    }

    [Fact]
    public void Missing_effects_file_means_unmapped()
    {
        using var temp = CreateData();
        Assert.Equal(MappingStatus.Unmapped, CardRepository.Load(temp.Root).StatusOf("vanguard-sergeant"));
    }

    [Fact]
    public void Unknown_action_error_names_the_file()
    {
        using var temp = CreateData();
        temp.Write("effects/kharox.json", """
            { "cardId": "kharox", "status": "Full", "abilities": [ { "kind": "Spell", "steps": [ { "action": "Teleport" } ] } ] }
            """);

        var error = Assert.Throws<InvalidDataException>(() => CardRepository.Load(temp.Root));
        Assert.Contains("kharox.json", error.Message);
    }

    [Fact]
    public void Typo_property_error_names_the_file()
    {
        using var temp = CreateData();
        temp.Write("effects/kharox.json", """{ "cardId": "kharox", "stauts": "Full" }""");

        var error = Assert.Throws<InvalidDataException>(() => CardRepository.Load(temp.Root));
        Assert.Contains("kharox.json", error.Message);
    }

    [Fact]
    public void Missing_kind_error_names_the_file()
    {
        using var temp = CreateData();
        temp.Write("effects/kharox.json", """{ "cardId": "kharox", "status": "Full", "abilities": [ { "steps": [] } ] }""");

        var error = Assert.Throws<InvalidDataException>(() => CardRepository.Load(temp.Root));
        Assert.Contains("kharox.json", error.Message);
    }

    [Fact]
    public void Duplicate_card_id_across_cards_and_tokens_throws()
    {
        using var temp = CreateData();
        temp.Write("tokens.json", """[ { "id": "kharox", "name": "Kharox", "type": "Unit", "text": {} } ]""");

        var error = Assert.Throws<InvalidDataException>(() => CardRepository.Load(temp.Root));
        Assert.Contains("kharox", error.Message);
    }

    [Fact]
    public void Duplicate_effects_card_id_throws()
    {
        using var temp = CreateData();
        temp.Write("effects/kharox-copy.json", """{ "cardId": "kharox", "status": "Full" }""");

        var error = Assert.Throws<InvalidDataException>(() => CardRepository.Load(temp.Root));
        Assert.Contains("kharox", error.Message);
    }
}
