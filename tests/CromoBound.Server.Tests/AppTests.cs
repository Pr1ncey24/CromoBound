using System.Net;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>The Blazor app (spec §4): every route and file of it needs a signed-in player, and nothing else becomes the app.</summary>
public class AppTests
{
    private const string AppScript = "_framework/blazor.webassembly.js";

    private static async Task<HttpClient> PlayerAsync(ServerFactory factory)
    {
        await factory.AddUserAsync("player1");
        return await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/decks")]
    [InlineData("/admin/users")]
    [InlineData("/admin/maintenance")]
    [InlineData("/match/6f9619ff-8b86-d011-b42d-00cf4fc964ff")]
    [InlineData("/DECKS")]
    public async Task Every_app_route_serves_the_app_to_a_signed_in_player(string route)
    {
        using var factory = new ServerFactory();
        var player = await PlayerAsync(factory);

        var response = await player.GetAsync(route);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(AppScript, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task The_app_script_is_served_to_a_signed_in_player()
    {
        using var factory = new ServerFactory();
        var player = await PlayerAsync(factory);

        var response = await player.GetAsync("/" + AppScript);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Signed_out_the_app_pages_redirect_to_login_and_its_files_are_a_bare_401()
    {
        using var factory = new ServerFactory();
        var client = factory.NewClient();

        foreach (var route in new[] { "/", "/decks", "/index.html" })
        {
            using var page = new HttpRequestMessage(HttpMethod.Get, route);
            page.Headers.Accept.ParseAdd("text/html");
            var redirected = await client.SendAsync(page);
            Assert.Equal(HttpStatusCode.Redirect, redirected.StatusCode);
            Assert.Equal("/login", redirected.Headers.Location?.OriginalString);
        }
        foreach (var file in new[] { "/index.html", "/" + AppScript, "/_framework/dotnet.js" })
        {
            var response = await client.GetAsync(file);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
    }

    [Fact]
    public async Task Paths_that_arent_app_routes_are_not_found()
    {
        using var factory = new ServerFactory();
        var player = await PlayerAsync(factory);

        foreach (var path in new[] { "/nothing", "/api/nothing", "/match/not-a-guid", "/decks/extra", "/admin" })
            Assert.Equal(HttpStatusCode.NotFound, (await player.GetAsync(path)).StatusCode);
    }

    [Fact]
    public void The_app_files_are_endpoints_so_default_deny_covers_them()
    {
        using var factory = new ServerFactory();

        var routes = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText?.TrimStart('/')).ToList();

        Assert.Contains(AppScript, routes);
        Assert.Contains("index.html", routes);
    }
}
