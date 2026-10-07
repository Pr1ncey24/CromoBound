using CromoBound.Importer;
using CromoBound.Models.Json;

namespace CromoBound.Models.Tests;

public class SchemaTests
{
    [Fact]
    public void Generated_schemas_match_committed_files()
    {
        foreach (var (name, content) in SchemaGenerator.GenerateAll())
        {
            var path = Path.Combine(RepoPaths.Schema, name);
            Assert.True(File.Exists(path), $"{name} is missing. Run: dotnet run --project tools/CromoBound.Importer -- schema");
            Assert.True(content == File.ReadAllText(path).ReplaceLineEndings("\n"),
                $"{name} is out of date. Run: dotnet run --project tools/CromoBound.Importer -- schema");
        }
    }

    [Fact]
    public void Effects_schema_describes_steps_and_union_shapes()
    {
        var schema = SchemaGenerator.Generate(typeof(CromoBound.Models.Effects.EffectsFile));

        Assert.StartsWith("{\n  \"$schema\": \"https://json-schema.org/draft/2020-12/schema\"", schema);
        Assert.Contains("\"BecameEmpowered\"", schema);
        Assert.Contains("\"ChoosePlayer\"", schema);
        Assert.Contains("\"oneOf\"", schema);
    }

    [Fact]
    public void RepoLocator_finds_root_from_subdirectory() =>
        Assert.Equal(RepoPaths.Root, RepoLocator.FindRoot(Path.Combine(RepoPaths.Root, "src", "CromoBound.Models")));

    [Fact]
    public void RepoLocator_throws_outside_repo()
    {
        using var temp = new TempDir();
        Assert.Throws<DirectoryNotFoundException>(() => RepoLocator.FindRoot(temp.Root));
    }
}
