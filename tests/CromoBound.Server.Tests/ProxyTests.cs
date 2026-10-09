using System.Net;
using System.Net.Http.Json;
using CromoBound.Server.Accounts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>The login rate limit behind the reverse proxy: forwarded client addresses count only from trusted proxies.</summary>
public class ProxyTests
{
    private const string PeerHeader = "X-Test-Peer";
    private const string DefaultPeer = "203.0.113.9";

    /// <summary>Gives each test request the connection address named by <see cref="PeerHeader"/> (an outside address otherwise),
    /// as the connection would have before the server's pipeline runs.</summary>
    private sealed class PeerAddress : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                var peer = context.Request.Headers[PeerHeader].FirstOrDefault() ?? DefaultPeer;
                context.Request.Headers.Remove(PeerHeader);
                context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
                return nextMiddleware(context);
            });
            next(app);
        };
    }

    private static ServerFactory Factory(Dictionary<string, string?> settings)
    {
        settings["CromoBound:LoginRequestsPerMinute"] = "2";
        return new ServerFactory(settings, services => services.AddSingleton<IStartupFilter, PeerAddress>());
    }

    private static async Task<HttpStatusCode> TryAsync(ServerFactory factory, string? peer, string? forwardedFor)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/login") { Content = JsonContent.Create(new LoginRequest("nobody-here", "wrong-password-1")) };
        if (peer is not null) request.Headers.Add(PeerHeader, peer);
        if (forwardedFor is not null) request.Headers.Add("X-Forwarded-For", forwardedFor);
        return (await factory.NewClient().SendAsync(request)).StatusCode;
    }

    [Fact]
    public async Task An_untrusted_peer_cant_spread_its_logins_over_spoofed_forwarded_addresses()
    {
        using var factory = Factory([]);

        Assert.Equal(HttpStatusCode.Unauthorized, await TryAsync(factory, null, "198.51.100.1"));
        Assert.Equal(HttpStatusCode.Unauthorized, await TryAsync(factory, null, "198.51.100.2"));

        Assert.Equal(HttpStatusCode.TooManyRequests, await TryAsync(factory, null, "198.51.100.3"));
    }

    [Fact]
    public async Task Behind_a_known_proxy_each_forwarded_client_has_its_own_limit()
    {
        using var factory = Factory(new() { ["CromoBound:KnownProxies:0"] = "10.0.0.1" });

        foreach (var client in new[] { "198.51.100.1", "198.51.100.2", "198.51.100.1", "198.51.100.2" })
            Assert.Equal(HttpStatusCode.Unauthorized, await TryAsync(factory, "10.0.0.1", client));

        Assert.Equal(HttpStatusCode.TooManyRequests, await TryAsync(factory, "10.0.0.1", "198.51.100.1"));
    }

    [Fact]
    public async Task A_known_network_trusts_the_proxies_inside_it()
    {
        using var factory = Factory(new() { ["CromoBound:KnownNetworks:0"] = "10.1.0.0/16" });

        foreach (var client in new[] { "198.51.100.1", "198.51.100.2", "198.51.100.1", "198.51.100.2" })
            Assert.Equal(HttpStatusCode.Unauthorized, await TryAsync(factory, "10.1.2.3", client));

        Assert.Equal(HttpStatusCode.TooManyRequests, await TryAsync(factory, "10.1.2.3", "198.51.100.1"));
    }

    [Theory]
    [InlineData("CromoBound:KnownProxies:0", "10.0.0.300")]
    [InlineData("CromoBound:KnownNetworks:0", "10.1.0.0/40")]
    [InlineData("CromoBound:KnownNetworks:0", "10.1.0.0")]
    public void A_bad_proxy_setting_stops_the_server_and_names_the_setting(string key, string value)
    {
        using var factory = new ServerFactory(new() { [key] = value });

        var error = Assert.ThrowsAny<Exception>(() => factory.Services);

        Assert.Contains(key[..key.LastIndexOf(':')], error.ToString());
        Assert.Contains(value, error.ToString());
    }
}
