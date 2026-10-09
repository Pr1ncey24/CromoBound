using System.Net;
using System.Net.Http.Json;
using CromoBound.Server.Accounts;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

public class ErrorTests
{
    /// <summary>Hashes normally (so the first admin is created) but fails when checking a password.</summary>
    private sealed class BrokenHasher : PasswordHasher<UserEntity>
    {
        public override PasswordVerificationResult VerifyHashedPassword(UserEntity user, string hashedPassword, string providedPassword) =>
            throw new InvalidOperationException("secret detail from inside the server");
    }

    [Fact]
    public async Task An_unexpected_error_returns_a_generic_500_without_details()
    {
        using var factory = new ServerFactory(services: services => services.AddSingleton<IPasswordHasher<UserEntity>, BrokenHasher>());

        var response = await factory.NewClient().PostAsJsonAsync("/login", new LoginRequest(ServerFactory.AdminName, ServerFactory.AdminPassword));

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("Something went wrong.", await response.Content.ReadAsStringAsync());
        Assert.Equal("noindex, nofollow", Assert.Single(response.Headers.GetValues("X-Robots-Tag")));
    }
}
