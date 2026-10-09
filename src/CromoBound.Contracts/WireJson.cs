using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using CromoBound.Models.Json;

namespace CromoBound.Contracts;

/// <summary>What the hub speaks and how saved match records are written: the engine's JSON settings (<see cref="CromoJson.Options"/>),
/// same names, polymorphic type names and enums as strings, but unindented, and with empty lists written as <c>[]</c> instead of left
/// out, so whatever the client or a restart reads back is exactly what was written.</summary>
public static class WireJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(CromoJson.Options)
        {
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.MakeReadOnly();
        return options;
    }
}
