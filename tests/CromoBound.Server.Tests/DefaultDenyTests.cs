using System.Collections.Concurrent;
using System.Net;
using CromoBound.Server.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CromoBound.Server.Tests;

public class DefaultDenyTests
{
    /// <summary>Keeps every message any logger writes, whatever its category.</summary>
    private sealed class CapturingLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new Logger(this, categoryName);

        public void Dispose() { }

        private sealed class Logger(CapturingLogs logs, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                logs.Messages.Enqueue($"{category} {logLevel}: {formatter(state, exception)} {exception}");
        }
    }

    [Fact]
    public async Task Every_endpoint_but_the_login_endpoints_requires_a_role()
    {
        using var factory = new ServerFactory();
        var policies = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var fallback = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy;

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
            var policy = await AuthorizationPolicy.CombineAsync(policies, endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>()) ?? fallback;
            Assert.True(policy is not null, $"{route} has no policy.");
            var roles = policy.Requirements.OfType<RolesAuthorizationRequirement>().ToList();
            Assert.True(roles.Count > 0, $"{route} has no role requirement.");
            Assert.All(roles, requirement =>
            {
                Assert.NotEmpty(requirement.AllowedRoles);
                Assert.All(requirement.AllowedRoles, role => Assert.Contains(role, new[] { Roles.Seat, Roles.Steward }));
            });
        }
    }

    [Fact]
    public async Task Every_admin_endpoint_requires_the_admin_role_and_not_just_the_player_role()
    {
        using var factory = new ServerFactory();
        var policies = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var fallback = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.FallbackPolicy;

        var admin = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText?.StartsWith("/api/admin", StringComparison.Ordinal) == true).ToList();

        Assert.NotEmpty(admin);
        foreach (var endpoint in admin)
        {
            var route = endpoint.RoutePattern.RawText;
            var policy = await AuthorizationPolicy.CombineAsync(policies, endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                endpoint.Metadata.GetOrderedMetadata<AuthorizationPolicy>()) ?? fallback;
            Assert.True(policy is not null, $"{route} has no policy.");
            var roles = policy.Requirements.OfType<RolesAuthorizationRequirement>().ToList();
            Assert.True(roles.Any(r => r.AllowedRoles.Contains(Roles.Steward)), $"{route} doesn't require the admin role.");
            Assert.All(roles, requirement => Assert.Equal(new[] { Roles.Steward }, requirement.AllowedRoles));
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

    [Fact]
    public void A_bare_authorization_requirement_needs_the_player_role_too()
    {
        using var factory = new ServerFactory();

        var standard = factory.Services.GetRequiredService<IOptions<AuthorizationOptions>>().Value.DefaultPolicy;

        var roles = Assert.Single(standard.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Equal(new[] { Roles.Seat }, roles.AllowedRoles);
    }

    [Fact]
    public async Task Refused_requests_never_write_a_role_identifier_to_the_log()
    {
        var logs = new CapturingLogs();
        using var factory = new ServerFactory(new() { ["Logging:LogLevel:Default"] = "Trace", ["Logging:LogLevel:Microsoft.AspNetCore"] = "Information" },
            services => services.AddSingleton<ILoggerProvider>(logs));
        await factory.AddUserAsync("player1");
        var player = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);

        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.NewClient().GetAsync("/api/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await player.GetAsync("/api/admin/users")).StatusCode);

        Assert.NotEmpty(logs.Messages);
        foreach (var message in logs.Messages)
        {
            Assert.DoesNotContain(Roles.Seat, message);
            Assert.DoesNotContain(Roles.Steward, message);
        }
    }
}
