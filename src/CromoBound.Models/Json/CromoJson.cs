using System.Collections;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace CromoBound.Models.Json;

/// <summary>Shared serializer settings for every JSON file CromoBound owns.</summary>
public static class CromoJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new JsonException($"JSON for {typeof(T).Name} was null.");

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
            NewLine = "\n",
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowOutOfOrderMetadataProperties = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver { Modifiers = { SkipEmptyCollections } },
        };
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        options.MakeReadOnly();
        return options;
    }

    private static void SkipEmptyCollections(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object) return;
        foreach (var property in typeInfo.Properties)
        {
            if (property.PropertyType == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(property.PropertyType)) continue;
            if (property.AttributeProvider?.IsDefined(typeof(KeepEmptyAttribute), inherit: true) == true) continue;
            property.ShouldSerialize = (_, value) => value is not ICollection { Count: 0 };
        }
    }
}
