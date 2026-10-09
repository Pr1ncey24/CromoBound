using System.Net;
using System.Net.Http.Json;
using System.Text;
using CromoBound.Contracts;
using CromoBound.Server.Accounts;

namespace CromoBound.Server.Tests;

public class SignInTests
{
    private static HttpRequestMessage Page(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Accept.ParseAdd("text/html");
        return request;
    }

    private static FormUrlEncodedContent Form(string userName, string password) =>
        new(new Dictionary<string, string> { ["userName"] = userName, ["password"] = password });

    [Fact]
    public async Task Signed_out_pages_go_to_the_login_page_and_everything_else_gets_401()
    {
        using var factory = new ServerFactory();
        var client = factory.NewClient();

        foreach (var path in new[] { "/", "/no-such-page" })
        {
            var response = await client.SendAsync(Page(path));
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/login", response.Headers.Location?.OriginalString);
            Assert.Equal("noindex, nofollow", Assert.Single(response.Headers.GetValues("X-Robots-Tag")));
        }
        var unauthorized = await client.GetAsync("/api/me");
        Assert.Equal(HttpStatusCode.Unauthorized, unauthorized.StatusCode);
        Assert.Equal("noindex, nofollow", Assert.Single(unauthorized.Headers.GetValues("X-Robots-Tag")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Page("/api/me"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/logout", null)).StatusCode);
    }

    [Fact]
    public async Task The_login_page_is_public_and_doesnt_say_what_the_site_is()
    {
        using var factory = new ServerFactory();

        var response = await factory.NewClient().SendAsync(Page("/login"));
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<form method=\"post\" action=\"/login\">", html);
        Assert.DoesNotContain("cromobound", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("riftbound", html, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("noindex, nofollow", Assert.Single(response.Headers.GetValues("X-Robots-Tag")));
    }

    [Fact]
    public async Task Me_says_who_you_are_and_whether_you_manage_users()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("player1");

        var admin = await (await factory.SignInAdminAsync()).GetFromJsonAsync<MeResponse>("/api/me");
        var player = await (await factory.SignInAsync("player1", ServerFactory.PlayerPassword)).GetFromJsonAsync<MeResponse>("/api/me");

        Assert.Equal(new MeResponse(ServerFactory.AdminName, true), admin);
        Assert.Equal(new MeResponse("player1", false), player);
    }

    [Fact]
    public async Task Signing_in_ignores_the_case_of_the_username()
    {
        using var factory = new ServerFactory();

        var client = await factory.SignInAsync(ServerFactory.AdminName.ToUpperInvariant(), ServerFactory.AdminPassword);

        Assert.Equal(ServerFactory.AdminName, (await client.GetFromJsonAsync<MeResponse>("/api/me"))!.UserName);
    }

    [Fact]
    public async Task A_form_sign_in_goes_home_and_a_failed_one_shows_the_generic_message()
    {
        using var factory = new ServerFactory();
        var client = factory.NewClient();

        var signedIn = await client.PostAsync("/login", Form(ServerFactory.AdminName, ServerFactory.AdminPassword));
        var home = await client.SendAsync(Page("/"));
        var failed = await factory.NewClient().PostAsync("/login", Form(ServerFactory.AdminName, "wrong-password-1"));

        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal("/", signedIn.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("_framework/blazor.webassembly.js", await home.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, failed.StatusCode);
        Assert.Contains(LoginEndpoints.Failure, await failed.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_failed_sign_in_gives_the_same_answer_whatever_the_reason()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("gone");
        await factory.WithStoreAsync(async store => await store.SetDisabledAsync((await store.FindAsync("gone"))!, true));
        var answers = new List<(HttpStatusCode Status, string Body)>();

        foreach (var (userName, password) in new[]
        {
            (ServerFactory.AdminName, "wrong-password-1"),
            ("nobody-here", "wrong-password-1"),
            ("gone", ServerFactory.PlayerPassword),
        })
        {
            var response = await factory.NewClient().PostAsJsonAsync("/login", new LoginRequest(userName, password));
            answers.Add((response.StatusCode, await response.Content.ReadAsStringAsync()));
        }

        Assert.All(answers, answer => Assert.Equal(answers[0], answer));
        Assert.Equal(HttpStatusCode.Unauthorized, answers[0].Status);
        Assert.Contains(LoginEndpoints.Failure, answers[0].Body);
    }

    [Theory]
    [InlineData("not json", "application/json")]
    [InlineData("""{ "userName": 5 }""", "application/json")]
    [InlineData("userName=admin", "text/plain")]
    [InlineData("userName=admin&password=x", "multipart/form-data")]
    public async Task A_malformed_sign_in_is_a_plain_failure(string body, string contentType)
    {
        using var factory = new ServerFactory();

        var response = await factory.NewClient().PostAsync("/login", new StringContent(body, Encoding.UTF8, contentType));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(LoginEndpoints.Failure, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task An_oversize_sign_in_is_a_plain_failure()
    {
        using var factory = new ServerFactory();
        var padding = new string('x', 1024 * 1024);

        var huge = await factory.NewClient().PostAsJsonAsync("/login", new LoginRequest(ServerFactory.AdminName, padding));
        var padded = await factory.NewClient().PostAsync("/login", new StringContent(
            $$"""{ "userName": "{{ServerFactory.AdminName}}", "password": "{{ServerFactory.AdminPassword}}", "padding": "{{padding}}" }""",
            Encoding.UTF8, "application/json"));
        var form = await factory.NewClient().PostAsync("/login", new StringContent(
            $"userName={ServerFactory.AdminName}&password={ServerFactory.AdminPassword}&padding={padding}", Encoding.UTF8, "application/x-www-form-urlencoded"));

        Assert.Contains(huge.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.RequestEntityTooLarge });
        Assert.Contains(padded.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.RequestEntityTooLarge });
        Assert.Contains(form.StatusCode, new[] { HttpStatusCode.Unauthorized, HttpStatusCode.RequestEntityTooLarge });
        await factory.SignInAdminAsync();
    }

    [Fact]
    public async Task Pages_and_refusals_carry_the_hardening_headers()
    {
        using var factory = new ServerFactory();
        var client = factory.NewClient();

        foreach (var response in new[] { await client.SendAsync(Page("/login")), await client.GetAsync("/api/me") })
        {
            Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
            Assert.Equal("DENY", Assert.Single(response.Headers.GetValues("X-Frame-Options")));
            Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
            Assert.Equal("frame-ancestors 'none'", Assert.Single(response.Headers.GetValues("Content-Security-Policy")));
            Assert.True(response.Headers.CacheControl?.NoStore, $"{response.RequestMessage?.RequestUri} can be stored.");
        }
    }

    [Fact]
    public async Task The_session_cookie_is_http_only_secure_and_strict()
    {
        using var factory = new ServerFactory();

        var response = await factory.NewClient().PostAsJsonAsync("/login", new LoginRequest(ServerFactory.AdminName, ServerFactory.AdminPassword));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));

        Assert.StartsWith("__Host-session=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        using var factory = new ServerFactory();
        var client = await factory.SignInAdminAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/logout", null)).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task A_changed_account_ends_its_sessions_at_once()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("player1");
        await factory.AddUserAsync("player2");
        var first = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
        var second = await factory.SignInAsync("player2", ServerFactory.PlayerPassword);
        Assert.Equal(HttpStatusCode.OK, (await first.GetAsync("/api/me")).StatusCode);

        await factory.WithStoreAsync(async store =>
        {
            await store.SetPasswordAsync((await store.FindAsync("player1"))!, "a-brand-new-password");
            await store.SetDisabledAsync((await store.FindAsync("player2"))!, true);
        });

        Assert.Equal(HttpStatusCode.Unauthorized, (await first.GetAsync("/api/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/me")).StatusCode);
    }
}
