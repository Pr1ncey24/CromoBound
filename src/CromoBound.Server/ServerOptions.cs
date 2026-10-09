namespace CromoBound.Server;

/// <summary>Server settings from the "CromoBound" configuration section (spec §7). Environment variables override them
/// (e.g. <c>CromoBound__DatabasePath</c>).</summary>
internal sealed class ServerOptions
{
    public const string Section = "CromoBound";

    public string DatabasePath { get; set; } = "cromobound.db";
    public string DataFolder { get; set; } = "data";

    /// <summary>Where the keys that encrypt the session cookie are kept; unset means the framework's default folder.</summary>
    public string? KeysFolder { get; set; }

    public int LoginRequestsPerMinute { get; set; } = 10;
    public int LockoutFailures { get; set; } = 5;
    public int LockoutMinutes { get; set; } = 15;
    public int CookieHours { get; set; } = 12;

    /// <summary>Addresses of the reverse proxies whose forwarded headers are trusted.</summary>
    public List<string> KnownProxies { get; set; } = [];
}
