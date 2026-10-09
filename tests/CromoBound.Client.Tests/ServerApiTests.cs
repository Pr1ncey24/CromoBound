using System.Net;
using CromoBound.Client.Services;
using CromoBound.Client.Tests.Fakes;

namespace CromoBound.Client.Tests;

public class ServerApiTests
{
    private static (ServerApi Api, SessionState Session, StubHandler Handler) Api(HttpStatusCode status, string body = "")
    {
        var handler = new StubHandler(status, body);
        var session = new SessionState();
        return (new ServerApi(new HttpClient(handler) { BaseAddress = new Uri("https://play.test/") }, session), session, handler);
    }

    [Fact]
    public async Task Me_reads_the_signed_in_user()
    {
        var (api, _, handler) = Api(HttpStatusCode.OK, """{"userName":"marco","canManageUsers":true}""");

        var me = await api.MeAsync();

        Assert.True(me.Ok);
        Assert.Equal(("marco", true), (me.Value!.UserName, me.Value.CanManageUsers));
        Assert.Equal("https://play.test/api/me", handler.Requests.Single().RequestUri!.ToString());
    }

    [Fact]
    public async Task A_401_ends_the_session_once()
    {
        var (api, session, _) = Api(HttpStatusCode.Unauthorized);
        var ended = 0;
        session.SessionEnded += () => ended++;

        var first = await api.MeAsync();
        var second = await api.UsersAsync();

        Assert.Equal(SessionState.Ended, first.Error);
        Assert.Equal(SessionState.Ended, second.Error);
        Assert.True(session.IsEnded);
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task A_refusal_carries_the_servers_message()
    {
        var (api, _, _) = Api(HttpStatusCode.BadRequest, """{"error":"That username is taken."}""");

        var result = await api.CreateUserAsync(new("giulia", "a-long-password-1", false));

        Assert.Equal("That username is taken.", result.Error);
    }

    [Fact]
    public async Task A_403_is_a_plain_refusal_and_anything_else_is_generic()
    {
        var (forbidden, _, _) = Api(HttpStatusCode.Forbidden);
        var (broken, _, _) = Api(HttpStatusCode.InternalServerError, "oops");

        Assert.Equal(ServerApi.Forbidden, (await forbidden.UsersAsync()).Error);
        Assert.Equal(ServerApi.Unexpected, (await broken.UsersAsync()).Error);
    }

    [Fact]
    public async Task A_timed_out_request_is_not_reachable_and_does_not_throw()
    {
        var api = new ServerApi(new HttpClient(new ThrowingHandler(new TaskCanceledException("timed out"))) { BaseAddress = new Uri("https://play.test/") }, new SessionState());

        Assert.Equal(ServerApi.NotReachable, (await api.MeAsync()).Error);
        Assert.Equal(ServerApi.NotReachable, (await api.LogoutAsync()).Error);
    }

    [Fact]
    public async Task Commands_send_the_right_method_route_and_body()
    {
        var (api, _, handler) = Api(HttpStatusCode.NoContent);

        await api.SetDisabledAsync(7, true);
        await api.LogoutAsync();

        Assert.Equal((HttpMethod.Put, "https://play.test/api/admin/users/7/disabled"), (handler.Requests[0].Method, handler.Requests[0].RequestUri!.ToString()));
        Assert.Equal("""{"disabled":true}""", await handler.Requests[0].Content!.ReadAsStringAsync());
        Assert.Equal((HttpMethod.Post, "https://play.test/logout"), (handler.Requests[1].Method, handler.Requests[1].RequestUri!.ToString()));
    }
}
