using CromoBound.Engine.Effects;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Engine.Tests;

/// <summary>Pins the exact forms <see cref="EffectsSupport"/> lets through for values, conditions and filters.</summary>
public class EffectsSupportTests
{
    [Theory]
    [InlineData("""{ "var": "picked" }""")]
    [InlineData("""{ "prop": "Might", "of": { "ref": "Self" } }""")]
    [InlineData("""{ "prop": "EmpowerCount", "of": { "ref": "Host" } }""")]
    [InlineData("""{ "prop": "Damage", "of": { "var": "picked" } }""")]
    [InlineData("""{ "mul": [2, { "var": "body" }] }""")]
    [InlineData("""{ "sum": [1, { "var": "body" }] }""")]
    public void Supported_values_are_let_through(string json) =>
        Assert.True(EffectsSupport.IsSupportedValue(CromoJson.Deserialize<Value>(json)));

    [Theory]
    [InlineData("""{ "prop": "Might", "of": { "ref": "Controller" } }""")]
    [InlineData("""{ "prop": "PowerCost", "of": { "ref": "Self" } }""")]
    [InlineData("""{ "sum": [] }""")]
    [InlineData("""{ "mul": [] }""")]
    [InlineData("""{ "sum": [1, { "min": [1, 2] }] }""")]
    [InlineData("""{ "min": [1, 2] }""")]
    public void Unsupported_values_are_refused(string json) =>
        Assert.False(EffectsSupport.IsSupportedValue(CromoJson.Deserialize<Value>(json)));

    [Theory]
    [InlineData("""{ "exists": { "select": "Unit", "all": true, "filter": { "relation": "Friendly", "mighty": true } } }""")]
    [InlineData("""{ "exists": { "ref": "Self" } }""")]
    [InlineData("""{ "compare": [{ "prop": "Might", "of": { "ref": "Self" } }, "gte", 3] }""")]
    [InlineData("""{ "paid": "body" }""")]
    [InlineData("""{ "turnOf": "You" }""")]
    [InlineData("""{ "turnOf": "Opponent" }""")]
    [InlineData("""{ "not": { "paid": "body" } }""")]
    public void Supported_conditions_are_let_through(string json) =>
        Assert.True(EffectsSupport.IsSupportedCondition(CromoJson.Deserialize<Condition>(json)));

    [Theory]
    [InlineData("""{ "turnOf": "EachPlayer" }""")]
    [InlineData("""{ "turnOf": { "controllerOf": { "ref": "Self" } } }""")]
    [InlineData("""{ "compare": [{ "prop": "PowerCost", "of": { "ref": "Self" } }, "gte", 3] }""")]
    [InlineData("""{ "exists": { "select": "Unit", "all": true, "filter": { "tags": ["Mech"] } } }""")]
    [InlineData("""{ "exists": { "select": "Card", "all": true } }""")]
    [InlineData("""{ "level": 6 }""")]
    [InlineData("""{ "not": { "level": 6 } }""")]
    public void Unsupported_conditions_are_refused(string json) =>
        Assert.False(EffectsSupport.IsSupportedCondition(CromoJson.Deserialize<Condition>(json)));

    [Theory]
    [InlineData("""{ "relation": "Friendly", "mighty": true }""")]
    [InlineData("""{ "location": { "ref": "Here" } }""")]
    [InlineData("""{ "location": { "select": "Battlefield" } }""")]
    [InlineData("""{ "not": { "mighty": true } }""")]
    [InlineData("""{ "relation": "Enemy", "not": { "relation": "Friendly" } }""")]
    public void Supported_filters_are_let_through(string json) =>
        Assert.True(EffectsSupport.IsSupportedFilter(CromoJson.Deserialize<Filter>(json)));

    [Theory]
    [InlineData("""{ "location": { "ref": "Controller" } }""")]
    [InlineData("""{ "location": { "select": "Battlefield", "filter": { "relation": "Friendly" } } }""")]
    [InlineData("""{ "not": { "tags": ["Mech"] } }""")]
    [InlineData("""{ "tags": ["Mech"] }""")]
    [InlineData("""{ "relation": "Opponent" }""")]
    public void Unsupported_filters_are_refused(string json) =>
        Assert.False(EffectsSupport.IsSupportedFilter(CromoJson.Deserialize<Filter>(json)));

    [Theory]
    [InlineData("""{ "mighty": true }""")]
    [InlineData("""{ "location": { "ref": "Here" } }""")]
    [InlineData("""{ "not": { "relation": "Friendly" } }""")]
    public void A_choose_card_filter_stays_basic(string filter)
    {
        var file = CromoJson.Deserialize<EffectsFile>($$"""
            { "cardId": "spell", "status": "Full", "abilities": [ { "kind": "Spell", "steps": [
              { "action": "ChooseCard", "from": { "zone": "Trash" }, "filter": {{filter}} } ] } ] }
            """);

        Assert.Equal(new[] { "abilities[0].steps[0]: filter" }, EffectsSupport.Problems(file, Models.Cards.CardType.Spell));
    }
}
