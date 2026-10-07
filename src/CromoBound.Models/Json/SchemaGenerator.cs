using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using CromoBound.Models.Cards;
using CromoBound.Models.Effects;

namespace CromoBound.Models.Json;

/// <summary>Generates JSON Schemas from the C# models (the single source of truth).</summary>
public static class SchemaGenerator
{
    private const string Draft = "https://json-schema.org/draft/2020-12/schema";

    private static readonly JsonSerializerOptions Output = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static IReadOnlyDictionary<string, string> GenerateAll() => new SortedDictionary<string, string>(StringComparer.Ordinal)
    {
        ["card.schema.json"] = Generate(typeof(Card)),
        ["deck.schema.json"] = Generate(typeof(Deck)),
        ["effects.schema.json"] = Generate(typeof(EffectsFile)),
    };

    public static string Generate(Type type)
    {
        var exporterOptions = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = Transform,
        };
        var schema = CromoJson.Options.GetJsonSchemaAsNode(type, exporterOptions).AsObject();
        var root = new JsonObject { ["$schema"] = Draft };
        foreach (var (key, value) in schema.ToList())
        {
            schema.Remove(key);
            root[key] = value;
        }
        return root.ToJsonString(Output) + "\n";
    }

    // Types with custom converters have no structural schema; describe their JSON shapes here.
    private static JsonNode Transform(JsonSchemaExporterContext context, JsonNode schema)
    {
        var type = context.TypeInfo.Type;
        if (type == typeof(Value))
            return OneOf(new JsonObject { ["type"] = "integer" }, new JsonObject { ["type"] = "object" });
        if (type == typeof(PlayerRef))
            return OneOf(
                new JsonObject { ["enum"] = new JsonArray(Enum.GetNames<PlayerKind>().Select(n => (JsonNode?)JsonValue.Create(n)).ToArray()) },
                new JsonObject { ["type"] = "object" });
        if (type == typeof(LineRef))
            return OneOf(
                new JsonObject { ["type"] = "integer", ["minimum"] = 1 },
                new JsonObject { ["type"] = "array", ["minItems"] = 1, ["items"] = new JsonObject { ["type"] = "integer", ["minimum"] = 1 } });
        if (type == typeof(Comparison))
            return new JsonObject { ["type"] = "array", ["minItems"] = 3, ["maxItems"] = 3 };
        return schema;
    }

    private static JsonObject OneOf(params JsonNode[] options) => new() { ["oneOf"] = new JsonArray(options) };
}
