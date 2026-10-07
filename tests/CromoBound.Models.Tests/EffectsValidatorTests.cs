using CromoBound.Data;
using CromoBound.Models.Cards;

namespace CromoBound.Models.Tests;

public class EffectsValidatorTests
{
    private static readonly Card Kharox = TestDb.Card("kharox", "<p>[Empower] cost<br />When I become [Empowered], ...</p>", new CardCost { Energy = 6, Power = [] });

    private static IReadOnlyList<ValidationIssue> Validate(Card card, string json, string? fileName = null, params Card[] extra) =>
        EffectsValidator.Validate(TestDb.With(extra.Append(card), null, (fileName ?? card.Id + ".json", json)));

    private static string Spell(string steps) => $$"""
        { "cardId": "kharox", "status": "Full", "abilities": [ { "kind": "Spell", "line": 1, "steps": {{steps}} } ] }
        """;

    [Fact]
    public void Valid_kharox_file_has_no_issues() =>
        Assert.Empty(Validate(Kharox, EffectsJsonTests.Kharox));

    [Fact]
    public void Variable_used_before_store_is_reported()
    {
        var issues = Validate(Kharox, Spell("""[ { "action": "Burn", "player": { "var": "victim" }, "amount": 3 } ]"""));
        Assert.Contains(issues, i => i.Message.Contains("victim"));
    }

    [Fact]
    public void Variable_stored_inside_block_is_not_visible_after_it()
    {
        var issues = Validate(Kharox, Spell("""
            [ { "action": "Optional", "steps": [ { "action": "ChoosePlayer", "store": "x" } ] },
              { "action": "Burn", "player": { "var": "x" } } ]
            """));
        Assert.Contains(issues, i => i.Message.Contains("'x'"));
    }

    [Fact]
    public void ForEach_variable_is_visible_inside_its_steps() =>
        Assert.Empty(Validate(Kharox, Spell("""
            [ { "action": "ForEach", "selector": { "select": "Unit", "all": true }, "as": "u",
                "steps": [ { "action": "Kill", "target": { "var": "u" } } ] } ]
            """)));

    [Fact]
    public void Did_must_reference_a_stored_step()
    {
        var issues = Validate(Kharox, Spell("""
            [ { "action": "If", "condition": { "did": "discarded" }, "then": [ { "action": "Draw" } ] } ]
            """));
        Assert.Contains(issues, i => i.Message.Contains("discarded"));
    }

    [Fact]
    public void Paid_must_reference_an_additional_cost()
    {
        var issues = Validate(Kharox, """
            { "cardId": "kharox", "status": "Full",
              "additionalCosts": [ { "id": "keeper", "optional": true, "cost": { "power": ["Calm"] } } ],
              "abilities": [ { "kind": "Passive", "while": { "paid": "keepr" } } ] }
            """);
        Assert.Contains(issues, i => i.Message.Contains("keepr"));
    }

    [Fact]
    public void Duplicate_additional_cost_ids_are_reported()
    {
        var issues = Validate(Kharox, """
            { "cardId": "kharox", "status": "Full",
              "additionalCosts": [ { "id": "a", "cost": { "energy": 1 } }, { "id": "a", "cost": { "energy": 2 } } ] }
            """);
        Assert.Contains(issues, i => i.Message.Contains("'a'"));
    }

    [Fact]
    public void Line_out_of_range_is_reported()
    {
        var issues = Validate(Kharox, """
            { "cardId": "kharox", "status": "Full", "abilities": [ { "kind": "Spell", "line": 3 } ] }
            """);
        Assert.Contains(issues, i => i.Message.Contains("line 3"));
    }

    [Fact]
    public void PlayToken_must_reference_a_token_card()
    {
        var recruit = TestDb.Card("token-recruit", supertype: Supertype.Token);
        Assert.Empty(Validate(Kharox, Spell("""[ { "action": "PlayToken", "token": "token-recruit", "count": 2 } ]"""), null, recruit));
        var issues = Validate(Kharox, Spell("""[ { "action": "PlayToken", "token": "token-dragon" } ]"""), null, recruit);
        Assert.Contains(issues, i => i.Message.Contains("token-dragon"));
    }

    [Fact]
    public void File_name_must_match_card_id()
    {
        var issues = Validate(Kharox, """{ "cardId": "kharox", "status": "Full" }""", "kharoks.json");
        Assert.Contains(issues, i => i.Message.Contains("kharox.json"));
    }

    [Fact]
    public void Unknown_card_id_is_reported()
    {
        var issues = EffectsValidator.Validate(TestDb.With([Kharox], null, ("ghost.json", """{ "cardId": "ghost", "status": "Full" }""")));
        Assert.Contains(issues, i => i.Message.Contains("ghost"));
    }

    [Fact]
    public void Unknown_power_needs_override_unless_unmapped()
    {
        var showstopper = TestDb.Card("showstopper", "<p>Buff a friendly unit.</p>", new CardCost { Energy = 1, Power = null });

        Assert.NotEmpty(Validate(showstopper, """{ "cardId": "showstopper", "status": "Full" }"""));
        Assert.Empty(Validate(showstopper, """{ "cardId": "showstopper", "status": "Unmapped" }"""));
        Assert.Empty(Validate(showstopper, """
            { "cardId": "showstopper", "status": "Full", "overrides": { "cost": { "energy": 1, "power": ["Body"] } } }
            """));
    }

    [Fact]
    public void Script_step_needs_a_script_name()
    {
        var issues = Validate(Kharox, Spell("""[ { "action": "Script" } ]"""));
        Assert.Contains(issues, i => i.Message.Contains("script"));
    }

    [Fact]
    public void Printing_with_unknown_card_is_reported()
    {
        var printing = new Printing { Id = "p1", CardId = "ghost", Set = "OGN" };
        var issues = EffectsValidator.Validate(TestDb.With([Kharox], [printing]));
        Assert.Contains(issues, i => i.Message.Contains("ghost"));
    }
}
