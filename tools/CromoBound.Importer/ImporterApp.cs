namespace CromoBound.Importer;

public static class ImporterApp
{
    private const string Usage = "Usage: dotnet run --project tools/CromoBound.Importer -- <schema>";

    public static Task<int> RunAsync(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "";
        var root = RepoLocator.FindRoot(Directory.GetCurrentDirectory());
        switch (command)
        {
            case "schema":
                ImportCommands.WriteSchemas(Path.Combine(root, "schema"));
                Console.WriteLine("Wrote schema/*.json.");
                return Task.FromResult(0);
            default:
                Console.Error.WriteLine(Usage);
                return Task.FromResult(1);
        }
    }
}
