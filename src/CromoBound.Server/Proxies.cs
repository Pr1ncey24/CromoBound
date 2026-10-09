using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace CromoBound.Server;

internal static class Proxies
{
    /// <summary>Behind the reverse proxy (spec §4.4, §8): the client address and scheme come from the forwarded headers, trusted only
    /// from the configured proxies, so the login rate limit sees real client addresses.</summary>
    public static IServiceCollection AddCromoBoundProxies(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ServerOptions>>((forwarded, options) =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in options.Value.KnownProxies) forwarded.KnownProxies.Add(IPAddress.Parse(proxy));
        });
        return services;
    }
}
