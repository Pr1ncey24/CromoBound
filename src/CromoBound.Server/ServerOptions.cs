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

    /// <summary>Networks (CIDR, e.g. <c>172.16.0.0/12</c>) whose proxies' forwarded headers are trusted, for a proxy whose
    /// address isn't fixed.</summary>
    public List<string> KnownNetworks { get; set; } = [];

    /// <summary>Binds the section and checks the numbers at startup: a setting below one stops the server and names itself.</summary>
    public static void AddTo(IServiceCollection services, IConfiguration configuration)
    {
        var options = services.AddOptions<ServerOptions>().Bind(configuration.GetSection(Section));
        foreach (var (setting, value) in new (string, Func<ServerOptions, int>)[]
        {
            (nameof(LoginRequestsPerMinute), o => o.LoginRequestsPerMinute),
            (nameof(LockoutFailures), o => o.LockoutFailures),
            (nameof(LockoutMinutes), o => o.LockoutMinutes),
            (nameof(CookieHours), o => o.CookieHours),
        })
            options.Validate(o => value(o) >= 1, $"{Section}:{setting} must be at least 1.");
        options.ValidateOnStart();
    }
}
