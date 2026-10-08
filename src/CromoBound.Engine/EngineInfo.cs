using System.Reflection;

namespace CromoBound.Engine;

public static class EngineInfo
{
    /// <summary>Recorded in every saved match. A different value means the match can't be replayed (spec §6.7).</summary>
    public static string Version { get; } =
        typeof(EngineInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
}
