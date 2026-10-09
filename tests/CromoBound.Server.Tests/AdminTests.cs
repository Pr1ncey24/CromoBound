using System.Net;
using System.Net.Http.Json;
using System.Text;
using CromoBound.Server.Accounts;

namespace CromoBound.Server.Tests;

public class AdminTests
{
    private const string NewPassword = "a-new-password-1";

    private static async Task<UserSummary> CreateAsync(HttpClient admin, string userName, bool isAdmin = false)
    {
        var response = await admin.PostAsJsonAsync("/api/admin/users", new CreateUserRequest(userName, ServerFactory.PlayerPassword, isAdmin));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<UserSummary>())!;
    }

    [Fact]
    public async Task An_admin_creates_and_lists_users_without_their_password_hashes()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();

        var created = await CreateAsync(admin, "player1");
        var list = await admin.GetAsync("/api/admin/users");
        var json = await list.Content.ReadAsStringAsync();
        var users = (await list.Content.ReadFromJsonAsync<List<UserSummary>>())!;

        Assert.Equal(new UserSummary(created.Id, "player1", false, false), created);
        Assert.Equal(new[] { ServerFactory.AdminName, "player1" }, users.Select(u => u.UserName));
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
    }

    [Theory]
    [InlineData("ab", "long-enough-password", "A username has")]
    [InlineData("player1", "short", "A password has")]
    [InlineData("ADMIN", "long-enough-password", "That username is taken.")]
    public async Task Bad_names_short_passwords_and_taken_names_are_refused_with_a_reason(string userName, string password, string reason)
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();

        var response = await admin.PostAsJsonAsync("/api/admin/users", new CreateUserRequest(userName, password, false));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(reason, (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
    }

    [Fact]
    public async Task A_new_password_works_and_ends_the_users_sessions()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();
        var player = await CreateAsync(admin, "player1");
        var session = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);

        var changed = await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/password", new PasswordRequest(NewPassword));

        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/me")).StatusCode);
        await factory.SignInAsync("player1", NewPassword);
        var old = await factory.NewClient().PostAsJsonAsync("/login", new LoginRequest("player1", ServerFactory.PlayerPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, old.StatusCode);
    }

    [Fact]
    public async Task A_short_new_password_is_refused()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();
        var player = await CreateAsync(admin, "player1");

        var response = await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/password", new PasswordRequest("short"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
    }

    [Fact]
    public async Task Promoting_demoting_and_disabling_end_the_users_sessions()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();
        var player = await CreateAsync(admin, "player1");
        var session = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/role", new RoleRequest(true))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await session.GetAsync("/api/me")).StatusCode);
        var promoted = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
        Assert.True((await promoted.GetFromJsonAsync<MeResponse>("/api/me"))!.CanManageUsers);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/role", new RoleRequest(false))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await promoted.GetAsync("/api/me")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/disabled", new DisabledRequest(true))).StatusCode);
        var disabled = await factory.NewClient().PostAsJsonAsync("/login", new LoginRequest("player1", ServerFactory.PlayerPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, disabled.StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/disabled", new DisabledRequest(false))).StatusCode);
        await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
    }

    [Fact]
    public async Task An_admin_cant_demote_or_disable_themselves_and_stays_signed_in_after_changing_their_own_password()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();
        var self = (await admin.GetFromJsonAsync<List<UserSummary>>("/api/admin/users"))!.Single(u => u.UserName == ServerFactory.AdminName);

        var demote = await admin.PutAsJsonAsync($"/api/admin/users/{self.Id}/role", new RoleRequest(false));
        var disable = await admin.PutAsJsonAsync($"/api/admin/users/{self.Id}/disabled", new DisabledRequest(true));
        var password = await admin.PutAsJsonAsync($"/api/admin/users/{self.Id}/password", new PasswordRequest(NewPassword));

        Assert.Equal(HttpStatusCode.BadRequest, demote.StatusCode);
        Assert.Equal(AdminEndpoints.NotYourself, (await demote.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
        Assert.Equal(HttpStatusCode.BadRequest, disable.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, password.StatusCode);
        Assert.True((await admin.GetFromJsonAsync<MeResponse>("/api/me"))!.CanManageUsers);
    }

    [Fact]
    public async Task Unknown_users_are_not_found()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();

        var response = await admin.PutAsJsonAsync("/api/admin/users/999/password", new PasswordRequest(NewPassword));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task A_player_gets_a_bare_403_from_every_admin_endpoint_and_a_signed_out_client_a_401()
    {
        using var factory = new ServerFactory();
        await factory.AddUserAsync("player1");
        var player = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);
        var signedOut = factory.NewClient();
        var requests = new Func<HttpClient, Task<HttpResponseMessage>>[]
        {
            client => client.GetAsync("/api/admin/users"),
            client => client.PostAsJsonAsync("/api/admin/users", new CreateUserRequest("someone", NewPassword, true)),
            client => client.PutAsJsonAsync("/api/admin/users/1/password", new PasswordRequest(NewPassword)),
            client => client.PutAsJsonAsync("/api/admin/users/1/role", new RoleRequest(false)),
            client => client.PutAsJsonAsync("/api/admin/users/1/disabled", new DisabledRequest(true)),
        };

        foreach (var request in requests)
        {
            var response = await request(player);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
            var headers = string.Join("\n", response.Headers.Concat(response.Content.Headers).SelectMany(h => h.Value.Prepend(h.Key)));
            foreach (var secret in new[] { Roles.Seat, Roles.Steward, "seat", "steward", "admin" })
                Assert.DoesNotContain(secret, headers, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(HttpStatusCode.Unauthorized, (await request(signedOut)).StatusCode);
        }
        await factory.SignInAdminAsync();
    }

    [Fact]
    public async Task Admin_endpoints_accept_only_json_bodies()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();
        var form = new FormUrlEncodedContent(new Dictionary<string, string> { ["userName"] = "player1", ["password"] = NewPassword });

        var response = await admin.PostAsync("/api/admin/users", form);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Single((await admin.GetFromJsonAsync<List<UserSummary>>("/api/admin/users"))!);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{")]
    [InlineData("")]
    public async Task A_malformed_json_body_is_a_400_and_creates_nothing(string body)
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();

        var response = await admin.PostAsync("/api/admin/users", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Single((await admin.GetFromJsonAsync<List<UserSummary>>("/api/admin/users"))!);
    }

    [Theory]
    [InlineData("role", "Say whether the user is an admin.")]
    [InlineData("disabled", "Say whether the user is disabled.")]
    public async Task A_role_or_disabled_change_that_doesnt_say_which_is_refused(string change, string reason)
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();
        var player = await CreateAsync(admin, "player1");
        var session = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);

        var response = await admin.PutAsync($"/api/admin/users/{player.Id}/{change}", new StringContent("{}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(reason, (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/me")).StatusCode);
    }

    [Fact]
    public async Task Setting_a_role_or_disabled_flag_to_what_it_already_is_keeps_the_users_sessions()
    {
        using var factory = new ServerFactory();
        var admin = await factory.SignInAdminAsync();
        var player = await CreateAsync(admin, "player1");
        var session = await factory.SignInAsync("player1", ServerFactory.PlayerPassword);

        var role = await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/role", new RoleRequest(false));
        var disabled = await admin.PutAsJsonAsync($"/api/admin/users/{player.Id}/disabled", new DisabledRequest(false));

        Assert.Equal(HttpStatusCode.NoContent, role.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await session.GetAsync("/api/me")).StatusCode);
        Assert.Equal(new UserSummary(player.Id, "player1", false, false),
            (await admin.GetFromJsonAsync<List<UserSummary>>("/api/admin/users"))!.Single(u => u.Id == player.Id));
    }
}
