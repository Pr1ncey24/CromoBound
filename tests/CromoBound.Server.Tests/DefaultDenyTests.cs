using CromoBound.Server.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CromoBound.Server.Tests;

public class DefaultDenyTests
{
    [Fact]
    public void Every_endpoint_but_the_login_endpoints_requires_a_role()
    {
        using var factory = new ServerFactory();

        var endpoints = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();

        Assert.NotEmpty(endpoints);
        foreach (var endpoint in endpoints)
        {
            var route = endpoint.RoutePattern.RawText;
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                Assert.Equal("/login", route);
                continue;
            }
            Assert.True(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0, $"{route} has no role requirement.");
        }
    }

    [Fact]
    public void Anything_without_its_own_rule_needs_the_player_role()
    {
        using var factory = new ServerFactory();

        var fallback = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy;

        Assert.NotNull(fallback);
        var roles = Assert.Single(fallback.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Equal(new[] { Roles.Seat }, roles.AllowedRoles);
    }
}
