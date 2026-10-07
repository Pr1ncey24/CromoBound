namespace CromoBound.Models.Tests;

public class RepoPathsTests
{
    [Fact]
    public void Root_contains_solution_and_docs()
    {
        Assert.True(File.Exists(Path.Combine(RepoPaths.Root, "CromoBound.slnx")));
        Assert.True(Directory.Exists(Path.Combine(RepoPaths.Root, "docs")));
    }

    [Fact]
    public void TempDir_writes_nested_files_and_cleans_up()
    {
        string root;
        using (var temp = new TempDir())
        {
            root = temp.Root;
            var path = temp.Write("a/b.json", "{}");
            Assert.Equal("{}", File.ReadAllText(path));
        }
        Assert.False(Directory.Exists(root));
    }
}
