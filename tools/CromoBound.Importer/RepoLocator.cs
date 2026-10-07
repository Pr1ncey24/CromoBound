namespace CromoBound.Importer;

public static class RepoLocator
{
    public const string SolutionFile = "CromoBound.slnx";

    public static string FindRoot(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, SolutionFile)))
                return dir.FullName;
        throw new DirectoryNotFoundException($"{SolutionFile} not found in {startDirectory} or any parent directory.");
    }
}
