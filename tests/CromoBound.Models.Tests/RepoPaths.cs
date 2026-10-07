namespace CromoBound.Models.Tests;

public static class RepoPaths
{
    public static string Root { get; } = FindRoot();
    public static string Data => Path.Combine(Root, "data");
    public static string Schema => Path.Combine(Root, "schema");

    private static string FindRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "CromoBound.slnx")))
                return dir.FullName;
        throw new InvalidOperationException($"CromoBound.slnx not found above {AppContext.BaseDirectory}.");
    }
}
