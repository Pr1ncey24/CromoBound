using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace CromoBound.Server;

internal static class Proxies
{
    /// <summary>Behind the reverse proxy (spec §4.4, §8): the client address and scheme come from the forwarded headers, trusted only
    /// from the configured proxies and proxy networks, so the login rate limit sees real client addresses. A setting that can't be
    /// read stops the server and names itself.</summary>
    public static IServiceCollection AddCromoBoundProxies(this IServiceCollection services)
    {
        services.AddOptions<ForwardedHeadersOptions>().Configure<IOptions<ServerOptions>>((forwarded, options) =>
        {
            forwarded.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            foreach (var proxy in options.Value.KnownProxies)
                forwarded.KnownProxies.Add(Parse(proxy, IPAddress.Parse, nameof(ServerOptions.KnownProxies), "IP addresses"));
            foreach (var network in options.Value.KnownNetworks)
                forwarded.KnownIPNetworks.Add(Parse(network, System.Net.IPNetwork.Parse, nameof(ServerOptions.KnownNetworks), "networks in CIDR form (e.g. 172.16.0.0/12)"));
        });
        return services;
    }

    private static T Parse<T>(string value, Func<string, T> parse, string setting, string what)
    {
        try
        {
            return parse(value);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException($"{ServerOptions.Section}:{setting} must list {what}: '{value}' is not one.");
        }
    }
}
