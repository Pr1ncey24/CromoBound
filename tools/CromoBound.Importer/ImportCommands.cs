using CromoBound.Models.Json;

namespace CromoBound.Importer;

public static class ImportCommands
{
    public static void WriteSchemas(string schemaDir)
    {
        Directory.CreateDirectory(schemaDir);
        foreach (var (name, content) in SchemaGenerator.GenerateAll())
            File.WriteAllText(Path.Combine(schemaDir, name), content);
    }
}
