using System.Text.Json;
using CromoBound.Models.Effects;
using CromoBound.Models.Json;

namespace CromoBound.Models.Tests;

public class ReferenceJsonTests
{
    [Fact]
    public void Value_reads_literal() => Assert.Equal(3, CromoJson.Deserialize<Value>("3").Literal);

    [Fact]
    public void Value_reads_count()
    {
        var value = CromoJson.Deserialize<Value>("""{ "count": { "select": "Battlefield", "all": true, "filter": { "relation": "Friendly" } } }""");
        Assert.Equal(SelectKind.Battlefield, value.Count!.Select);
        Assert.Equal(Relation.Friendly, value.Count.Filter!.Relation);
    }

    [Fact]
    public void Value_reads_prop_and_var()
    {
        var prop = CromoJson.Deserialize<Value>("""{ "prop": "Might", "of": { "ref": "Self" } }""");
        Assert.Equal(ValueProperty.Might, prop.Prop);
        Assert.Equal(RefKind.Self, prop.Of!.Ref);
        Assert.Equal("x", CromoJson.Deserialize<Value>("""{ "var": "x" }""").Var);
    }

    [Fact]
    public void Value_reads_sum()
    {
        var value = CromoJson.Deserialize<Value>("""{ "sum": [1, { "var": "x" }] }""");
        Assert.Equal(1, value.Sum![0].Literal);
        Assert.Equal("x", value.Sum[1].Var);
    }

    [Theory]
    [InlineData("""{ "var": "x", "prop": "Might", "of": { "ref": "Self" } }""")]
    [InlineData("""{ "prop": "Might" }""")]
    [InlineData("""{ }""")]
    [InlineData("2.5")]
    [InlineData("\"three\"")]
    public void Value_rejects_invalid_shapes(string json) => Assert.Throws<JsonException>(() => CromoJson.Deserialize<Value>(json));

    [Fact]
    public void Value_writes_literal_as_number()
    {
        Value value = 4;
        Assert.Equal("4", CromoJson.Serialize(value));
    }

    [Fact]
    public void PlayerRef_reads_string_and_var()
    {
        Assert.Equal(PlayerKind.You, CromoJson.Deserialize<PlayerRef>("\"You\"").Kind);
        Assert.Equal("victim", CromoJson.Deserialize<PlayerRef>("""{ "var": "victim" }""").Var);
        Assert.Equal(RefKind.TriggerSubject, CromoJson.Deserialize<PlayerRef>("""{ "controllerOf": { "ref": "TriggerSubject" } }""").ControllerOf!.Ref);
    }

    [Theory]
    [InlineData("\"Nobody\"")]
    [InlineData("\"1\"")]
    [InlineData("""{ }""")]
    public void PlayerRef_rejects_invalid_shapes(string json) => Assert.Throws<JsonException>(() => CromoJson.Deserialize<PlayerRef>(json));

    [Fact]
    public void PlayerRef_round_trips()
    {
        Assert.Equal("\"You\"", CromoJson.Serialize(PlayerRef.You));
        Assert.Contains("\"var\": \"victim\"", CromoJson.Serialize(new PlayerRef { Var = "victim" }));
    }

    [Fact]
    public void LineRef_reads_number_and_array()
    {
        Assert.Equal(new[] { 2 }, CromoJson.Deserialize<LineRef>("2").Lines);
        Assert.Equal(new[] { 1, 2 }, CromoJson.Deserialize<LineRef>("[1, 2]").Lines);
        Assert.Equal("2", CromoJson.Serialize<LineRef>(2));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("[]")]
    [InlineData("[1, 0]")]
    public void LineRef_rejects_invalid_lines(string json) => Assert.Throws<JsonException>(() => CromoJson.Deserialize<LineRef>(json));

    [Fact]
    public void Comparison_reads_and_writes_triplet()
    {
        var comparison = CromoJson.Deserialize<Comparison>("""[3, "gte", { "var": "x" }]""");
        Assert.Equal(3, comparison.Left.Literal);
        Assert.Equal(CompareOp.Gte, comparison.Op);
        Assert.Equal("x", comparison.Right.Var);
        Assert.Contains("\"gte\"", CromoJson.Serialize(comparison));
    }

    [Theory]
    [InlineData("""[3, "bigger", 2]""")]
    [InlineData("""[3, "gte"]""")]
    [InlineData("""[3, "gte", 2, 1]""")]
    public void Comparison_rejects_invalid_shapes(string json) => Assert.Throws<JsonException>(() => CromoJson.Deserialize<Comparison>(json));

    [Fact]
    public void Condition_reads_single_key()
    {
        Assert.True(CromoJson.Deserialize<Condition>("""{ "legion": true }""").Legion);
        Assert.Equal(6, CromoJson.Deserialize<Condition>("""{ "level": 6 }""").Level);
        Assert.Equal(2, CromoJson.Deserialize<Condition>("""{ "all": [ { "legion": true }, { "level": 6 } ] }""").All!.Count);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "legion": true, "level": 6 }""")]
    public void Condition_requires_exactly_one_key(string json) => Assert.Throws<JsonException>(() => CromoJson.Deserialize<Condition>(json));

    [Fact]
    public void ObjectRef_reads_ref_var_and_selector()
    {
        Assert.Equal(RefKind.Self, CromoJson.Deserialize<ObjectRef>("""{ "ref": "Self" }""").Ref);
        Assert.Equal("picked", CromoJson.Deserialize<ObjectRef>("""{ "var": "picked" }""").Var);
        var selector = CromoJson.Deserialize<ObjectRef>("""{ "select": "Unit", "count": 1, "filter": { "might": { "lte": 3 } } }""");
        Assert.Equal(1, selector.Count);
        Assert.Equal(3, selector.Filter!.Might!.Lte!.Literal);
    }

    [Theory]
    [InlineData("""{ }""")]
    [InlineData("""{ "ref": "Self", "var": "x" }""")]
    [InlineData("""{ "ref": "Self", "count": 1 }""")]
    [InlineData("""{ "select": "Unit", "count": 1, "all": true }""")]
    public void ObjectRef_rejects_invalid_shapes(string json) => Assert.Throws<JsonException>(() => CromoJson.Deserialize<ObjectRef>(json));
}
