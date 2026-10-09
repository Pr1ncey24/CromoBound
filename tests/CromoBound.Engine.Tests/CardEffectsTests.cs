using CromoBound.Engine.Effects;
using CromoBound.Engine.Effects.Steps;
using CromoBound.Engine.State;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;
using static CromoBound.Engine.Tests.TestGame;

namespace CromoBound.Engine.Tests;

public class CardEffectsTests
{
    private const string DealOnLineTwo = """
        { "cardId": "multi-spell", "status": "Partial", "keywords": [ { "keyword": "Reaction" } ],
          "abilities": [ { "kind": "Spell", "line": 2,
            "steps": [ { "action": "Deal", "amount": 3, "target": { "select": "Unit", "count": 1 } } ] } ] }
        """;

    private const string FullTank = """{ "cardId": "unit-2", "status": "Full", "keywords": [ { "keyword": "Tank" } ] }""";

    [Fact]
    public void A_card_without_a_file_is_unmapped_with_text_keywords_and_every_line_manual()
    {
        var info = new CardEffects(EngineTestDb.Create()).For("tank-2");

        Assert.Equal(MappingStatus.Unmapped, info.Status);
        Assert.Contains(DisplayKeyword.Tank, info.Keywords);
        Assert.Equal(new[] { 1, 2 }, info.ManualLines);
        Assert.Empty(info.Abilities);
        Assert.Empty(info.Unsupported);
    }

    [Fact]
    public void A_full_file_brings_its_abilities_and_keywords_and_no_manual_lines()
    {
        var info = new CardEffects(EngineTestDb.Create(("unit-2", FullTank))).For("unit-2");

        Assert.Equal(MappingStatus.Full, info.Status);
        Assert.Equal(new[] { DisplayKeyword.Tank }, info.Keywords);
        Assert.Empty(info.ManualLines);
    }

    [Fact]
    public void A_partial_file_leaves_its_unmapped_lines_manual_but_not_keyword_lines()
    {
        var info = new CardEffects(EngineTestDb.Create(("multi-spell", DealOnLineTwo))).For("multi-spell");

        Assert.Equal(MappingStatus.Partial, info.Status);
        Assert.Equal(new[] { 3 }, info.ManualLines);
        Assert.Single(info.Abilities);
        Assert.Contains(DisplayKeyword.Reaction, info.Keywords);
    }

    [Fact]
    public void A_file_the_engine_cant_run_yet_falls_back_to_unmapped_and_says_why()
    {
        var db = EngineTestDb.Create(("tank-2", """
            { "cardId": "tank-2", "status": "Full", "keywords": [ { "keyword": "Repeat", "value": 1 } ],
              "abilities": [ { "kind": "Triggered", "trigger": { "event": "Hold" }, "steps": [ { "action": "Draw" } ] } ] }
            """));

        var info = new CardEffects(db).For("tank-2");

        Assert.Equal(MappingStatus.Unmapped, info.Status);
        Assert.Contains(DisplayKeyword.Tank, info.Keywords);
        Assert.Equal(new[] { "keyword Repeat", "abilities[0]: trigger" }, info.Unsupported);
    }

    [Theory]
    [InlineData("""{ "action": "ExtraTurn" }""", "abilities[0].steps[0]: step ExtraTurn")]
    [InlineData("""{ "action": "GainXp", "amount": { "var": "x" } }""", "abilities[0].steps[0]: value")]
    [InlineData("""{ "action": "Draw", "amount": { "var": "x" } }""", "abilities[0].steps[0]: value")]
    [InlineData("""{ "action": "Kill", "target": { "select": "Unit", "all": true } }""", "abilities[0].steps[0]: target")]
    [InlineData("""{ "action": "Kill", "target": { "select": "Unit", "count": 1, "filter": { "tags": ["Mech"] } } }""", "abilities[0].steps[0]: target")]
    [InlineData("""{ "action": "Draw", "player": { "controllerOf": { "ref": "Self" } } }""", "abilities[0].steps[0]: player")]
    [InlineData("""{ "action": "Deal", "amount": 2, "split": true, "target": { "select": "Unit", "count": 1 } }""", "abilities[0].steps[0]: split, bonus or source")]
    [InlineData("""{ "action": "Optional", "steps": [ { "action": "Draw" } ] }""", "abilities[0].steps[0]: optional")]
    [InlineData("""{ "action": "Optional", "reflexive": true, "steps": [ { "action": "Kill", "target": { "select": "Unit", "count": 1 } } ] }""", "abilities[0].steps[0].steps[0]: target")]
    [InlineData("""{ "action": "ChooseCard", "from": { "zone": "Hand" } }""", "abilities[0].steps[0]: from")]
    [InlineData("""{ "action": "ChoosePlayer", "filter": { "relation": "Friendly" } }""", "abilities[0].steps[0]: filter")]
    [InlineData("""{ "action": "Predict", "amount": 2 }""", "abilities[0].steps[0]: value")]
    [InlineData("""{ "action": "Kill", "player": "Opponent", "target": { "select": "Unit", "count": 1 } }""", "abilities[0].steps[0]: player")]
    [InlineData("""{ "action": "Deal", "amount": 1, "player": "You", "target": { "select": "Unit", "count": 1 } }""", "abilities[0].steps[0]: player")]
    public void Unsupported_steps_targets_values_and_players_are_named(string step, string problem)
    {
        var file = CromoJson.Deserialize<EffectsFile>(
            $$"""{ "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "steps": [ {{step}} ] } ] }""");

        Assert.Equal(new[] { problem }, EffectsSupport.Problems(file, CardType.Spell));
    }

    [Fact]
    public void Mapped_keywords_replace_the_text_ones_in_play()
    {
        var game = new TestGame(db: EngineTestDb.Create(("unit-2", FullTank)));
        var unit = game.Put("unit-2", Place.Base(P1));
        var engine = game.Start();

        Assert.True(engine.Has(game.State[unit], DisplayKeyword.Tank));
    }

    [Fact]
    public void Keywords_stand_for_triggered_abilities_listed_after_the_files_own()
    {
        var db = EngineTestDb.Create(("unit-3", """
            { "cardId": "unit-3", "status": "Full",
              "keywords": [ { "keyword": "Vision" }, { "keyword": "Hunt", "value": 2 },
                            { "keyword": "Deathknell", "steps": [ { "action": "Draw", "amount": 1 } ] } ],
              "abilities": [ { "kind": "Triggered", "trigger": { "event": "Dies", "subject": { "ref": "Self" } },
                               "steps": [ { "action": "Burn", "amount": 1 } ] } ] }
            """));

        var info = new CardEffects(db).For("unit-3");
        var abilities = info.Abilities.Cast<TriggeredAbility>().ToList();

        Assert.Equal(MappingStatus.Full, info.Status);
        Assert.Equal(
            new[] { TriggerEvent.Dies, TriggerEvent.Played, TriggerEvent.Conquer, TriggerEvent.Hold, TriggerEvent.Dies },
            abilities.Select(a => a.Trigger.Event));
        Assert.All(abilities, a => Assert.Equal(RefKind.Self, a.Trigger.Subject?.Ref));
        Assert.IsType<PredictStep>(Assert.Single(abilities[1].Steps));
        Assert.Equal(2, Assert.IsType<GainXpStep>(Assert.Single(abilities[3].Steps)).Amount.Literal);
        Assert.IsType<DrawStep>(Assert.Single(abilities[4].Steps));
    }

    [Theory]
    [InlineData("""{ "event": "Dies" }""")]
    [InlineData("""{ "event": "Attack", "subject": { "ref": "Self" } }""")]
    [InlineData("""{ "event": "Played", "by": "You", "where": { "ref": "Here" } }""")]
    public void Unsupported_triggers_are_named(string trigger)
    {
        var file = CromoJson.Deserialize<EffectsFile>(
            $$"""{ "cardId": "unit-3", "status": "Full", "abilities": [ { "kind": "Triggered", "trigger": {{trigger}}, "steps": [] } ] }""");

        Assert.Equal(new[] { "abilities[0]: trigger" }, EffectsSupport.Problems(file, CardType.Unit));
    }

    [Fact]
    public void A_triggered_ability_cant_have_target_selectors_yet()
    {
        var file = CromoJson.Deserialize<EffectsFile>("""
            { "cardId": "unit-3", "status": "Full", "abilities": [ { "kind": "Triggered",
              "trigger": { "event": "Dies", "subject": { "ref": "Self" } },
              "steps": [ { "action": "Kill", "target": { "select": "Unit", "count": 1 } } ] } ] }
            """);

        Assert.Equal(new[] { "abilities[0].steps[0]: target" }, EffectsSupport.Problems(file, CardType.Unit));
    }

    [Theory]
    [InlineData("""{ "keyword": "Tank", "value": 2 }""", "keywords[0]: value")]
    [InlineData("""{ "keyword": "Accelerate", "cost": { "energy": 1 } }""", "keywords[0]: cost")]
    [InlineData("""{ "keyword": "Vision", "steps": [ { "action": "Draw" } ] }""", "keywords[0]: steps")]
    [InlineData("""{ "keyword": "Hunt", "value": 1, "cost": { "energy": 1 } }""", "keywords[0]: cost")]
    [InlineData("""{ "keyword": "Empower", "cost": { "energy": 1, "exhaustSelf": true } }""", "keywords[0]: cost")]
    public void Keyword_parameters_the_engine_would_ignore_are_named(string keyword, string problem)
    {
        var file = CromoJson.Deserialize<EffectsFile>($$"""{ "cardId": "unit-3", "status": "Full", "keywords": [ {{keyword}} ] }""");

        Assert.Equal(new[] { problem }, EffectsSupport.Problems(file, CardType.Unit));
    }

    [Theory]
    [InlineData("""{ "event": "Hold", "subject": { "ref": "Self" } }""", CardType.Battlefield)]
    [InlineData("""{ "event": "Played", "subject": { "ref": "Self" } }""", CardType.Battlefield)]
    [InlineData("""{ "event": "Hold", "by": "You", "where": { "ref": "Here" } }""", CardType.Unit)]
    public void Trigger_forms_must_fit_the_card_type(string trigger, CardType type)
    {
        var file = CromoJson.Deserialize<EffectsFile>(
            $$"""{ "cardId": "unit-3", "status": "Full", "abilities": [ { "kind": "Triggered", "trigger": {{trigger}}, "steps": [] } ] }""");

        Assert.Equal(new[] { "abilities[0]: trigger" }, EffectsSupport.Problems(file, type));
    }

    [Fact]
    public void Every_mechanical_keyword_has_a_display_keyword()
    {
        Assert.All(Enum.GetValues<MechanicalKeyword>(),
            keyword => Assert.True(Enum.TryParse<DisplayKeyword>(keyword.ToString(), out _), $"{keyword} has no DisplayKeyword"));
    }

    [Fact]
    public void Mapped_cards_keep_their_keyword_entries_and_unmapped_ones_have_none()
    {
        var db = EngineTestDb.Create(("unit-2", FullTank));
        var effects = new CardEffects(db);

        Assert.Equal(MechanicalKeyword.Tank, Assert.Single(effects.For("unit-2").KeywordEntries).Keyword);
        Assert.Empty(effects.For("tank-2").KeywordEntries);
    }
}
