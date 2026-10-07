using System.Text.Json;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Models.Tests;

public class EffectsJsonTests
{
    public const string Kharox = """
        {
          "cardId": "kharox",
          "status": "Full",
          "keywords": [
            { "keyword": "Empower", "cost": { "energy": 6, "power": ["Chaos", "Chaos"] } }
          ],
          "abilities": [
            {
              "kind": "Triggered", "line": 2,
              "trigger": { "event": "BecameEmpowered", "subject": { "ref": "Self" } },
              "steps": [
                { "action": "ChoosePlayer", "filter": { "relation": "Opponent" }, "store": "victim" },
                { "action": "Burn", "player": { "var": "victim" }, "amount": 3 },
                { "action": "Optional", "reflexive": true, "steps": [
                  { "action": "ChooseCard",
                    "from": { "zone": "Trash", "owner": { "var": "victim" } },
                    "filter": { "type": "Unit" }, "count": 1, "store": "picked" },
                  { "action": "Play", "card": { "var": "picked" }, "cost": "IgnoreAll" }
                ] }
              ]
            }
          ]
        }
        """;

    [Fact]
    public void Kharox_deserializes_into_typed_structure()
    {
        var file = CromoJson.Deserialize<EffectsFile>(Kharox);

        Assert.Equal(MappingStatus.Full, file.Status);
        var empower = Assert.Single(file.Keywords);
        Assert.Equal(MechanicalKeyword.Empower, empower.Keyword);
        Assert.Equal(6, empower.Cost!.Energy);
        Assert.Equal(new[] { PowerSymbol.Chaos, PowerSymbol.Chaos }, empower.Cost.Power);

        var trigger = Assert.IsType<TriggeredAbility>(Assert.Single(file.Abilities));
        Assert.Equal(new[] { 2 }, trigger.Line!.Lines);
        Assert.Equal(TriggerEvent.BecameEmpowered, trigger.Trigger.Event);
        Assert.Equal(3, trigger.Steps.Count);

        var choose = Assert.IsType<ChoosePlayerStep>(trigger.Steps[0]);
        Assert.Equal("victim", choose.Store);
        Assert.Equal(Relation.Opponent, choose.Filter!.Relation);

        var burn = Assert.IsType<BurnStep>(trigger.Steps[1]);
        Assert.Equal("victim", burn.Player!.Var);
        Assert.Equal(3, burn.Amount.Literal);

        var optional = Assert.IsType<OptionalStep>(trigger.Steps[2]);
        Assert.True(optional.Reflexive);
        var chooseCard = Assert.IsType<ChooseCardStep>(optional.Steps[0]);
        Assert.Equal(Zone.Trash, chooseCard.From.Zone);
        Assert.Equal("victim", chooseCard.From.Owner!.Var);
        var play = Assert.IsType<PlayStep>(optional.Steps[1]);
        Assert.Equal("picked", play.Card.Var);
        Assert.Equal(PlayCostMode.IgnoreAll, play.Cost);
    }

    [Fact]
    public void Activated_ability_with_combined_cost_deserializes()
    {
        var file = CromoJson.Deserialize<EffectsFile>("""
            {
              "cardId": "garbage-grabber", "status": "Full",
              "abilities": [ { "kind": "Activated", "line": 1,
                "cost": { "energy": 1, "exhaustSelf": true,
                          "actions": [ { "action": "Recycle", "from": { "zone": "Trash", "owner": "You" }, "count": 3 } ] },
                "steps": [ { "action": "Draw", "player": "You", "amount": 1 } ] } ]
            }
            """);

        var ability = Assert.IsType<ActivatedAbility>(Assert.Single(file.Abilities));
        Assert.True(ability.Cost!.ExhaustSelf);
        var recycle = Assert.IsType<RecycleStep>(Assert.Single(ability.Cost.Actions));
        Assert.Equal(3, recycle.Count!.Literal);
        Assert.Equal(PlayerKind.You, recycle.From!.Owner!.Kind);
        Assert.IsType<DrawStep>(Assert.Single(ability.Steps));
    }

    [Fact]
    public void Additional_costs_and_passive_modifiers_deserialize()
    {
        var file = CromoJson.Deserialize<EffectsFile>("""
            {
              "cardId": "brazen-buccaneer", "status": "Full",
              "additionalCosts": [
                { "id": "discount", "optional": true,
                  "cost": { "actions": [ { "action": "Discard", "player": "You", "count": 1 } ] },
                  "modifiesCost": { "type": "CostReduction", "energy": 2 } }
              ],
              "abilities": [
                { "kind": "Passive", "condition": { "legion": true },
                  "modifiers": [ { "type": "Permission", "permission": "PlayToBattlefieldWithEnemyUnits", "appliesTo": { "ref": "Self" } } ] }
              ]
            }
            """);

        var cost = Assert.Single(file.AdditionalCosts);
        Assert.Equal("discount", cost.Id);
        var reduction = Assert.IsType<CostReductionModifier>(cost.ModifiesCost);
        Assert.Equal(2, reduction.Energy!.Literal);
        var passive = Assert.IsType<PassiveAbility>(Assert.Single(file.Abilities));
        Assert.True(passive.Condition!.Legion);
        Assert.IsType<PermissionModifier>(Assert.Single(passive.Modifiers));
    }

    [Fact]
    public void Unknown_action_is_rejected() =>
        Assert.Throws<JsonException>(() => CromoJson.Deserialize<EffectsFile>("""
            { "cardId": "x", "status": "Full", "abilities": [ { "kind": "Spell", "steps": [ { "action": "Teleport" } ] } ] }
            """));

    [Fact]
    public void Misspelled_step_property_is_rejected() =>
        Assert.Throws<JsonException>(() => CromoJson.Deserialize<EffectsFile>("""
            { "cardId": "x", "status": "Full", "abilities": [ { "kind": "Spell", "steps": [ { "action": "Draw", "ammount": 2 } ] } ] }
            """));

    [Fact]
    public void Discriminator_may_appear_after_other_properties()
    {
        var file = CromoJson.Deserialize<EffectsFile>("""
            { "cardId": "x", "status": "Full", "abilities": [ { "steps": [ { "amount": 4, "action": "Draw" } ], "kind": "Spell" } ] }
            """);

        var spell = Assert.IsType<SpellAbility>(Assert.Single(file.Abilities));
        Assert.Equal(4, Assert.IsType<DrawStep>(Assert.Single(spell.Steps)).Amount.Literal);
    }

    [Fact]
    public void Minimal_file_serializes_without_empty_lists()
    {
        var file = new EffectsFile { Schema = "../../schema/effects.schema.json", CardId = "vanguard-sergeant", Status = MappingStatus.Full };

        Assert.Equal("""
            {
              "$schema": "../../schema/effects.schema.json",
              "cardId": "vanguard-sergeant",
              "status": "Full"
            }
            """.ReplaceLineEndings("\n"), CromoJson.Serialize(file));
    }

    [Fact]
    public void Kharox_round_trip_is_stable()
    {
        var once = CromoJson.Serialize(CromoJson.Deserialize<EffectsFile>(Kharox));
        var twice = CromoJson.Serialize(CromoJson.Deserialize<EffectsFile>(once));
        Assert.Equal(once, twice);
    }
}
