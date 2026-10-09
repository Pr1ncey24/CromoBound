using System.Text.Json;
using CromoBound.Models.Json;

namespace CromoBound.Server;

/// <summary>The engine's JSON settings (<see cref="CromoJson.Options"/>) without indentation: what the hub speaks and how saved match
/// records are written. Same names, same polymorphic type names, same enums as strings.</summary>
internal static class ServerJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(CromoJson.Options) { WriteIndented = false };
        options.MakeReadOnly();
        return options;
    }
}
