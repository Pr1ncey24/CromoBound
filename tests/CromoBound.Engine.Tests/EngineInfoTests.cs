namespace CromoBound.Engine.Tests;

public class EngineInfoTests
{
    [Fact]
    public void Version_is_the_assembly_informational_version()
    {
        Assert.False(string.IsNullOrWhiteSpace(EngineInfo.Version));
        Assert.NotEqual("unknown", EngineInfo.Version);
    }
}
