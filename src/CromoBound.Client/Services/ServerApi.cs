using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CromoBound.Contracts;

namespace CromoBound.Client.Services;

/// <summary>The HTTP endpoints over the app's own origin, so the session cookie goes along. A 401 ends the session; a 403 is a plain
/// refusal; a refusal with a message shows that message; anything else is generic (spec §11).</summary>
public sealed class ServerApi(HttpClient http, SessionState session) : IServerApi
{
    public const string Forbidden = "You can't do that.";
    public const string Unexpected = "Something went wrong.";
    public const string NotReachable = "The server can't be reached right now.";

    public Task<ApiResult<MeResponse>> MeAsync() => QueryAsync<MeResponse>(HttpMethod.Get, "api/me", null);

    public Task<ApiResult> LogoutAsync() => CommandAsync(HttpMethod.Post, "logout", null);

    public Task<ApiResult<IReadOnlyList<UserSummary>>> UsersAsync() =>
        QueryAsync<IReadOnlyList<UserSummary>>(HttpMethod.Get, "api/admin/users", null);

    public Task<ApiResult<UserSummary>> CreateUserAsync(CreateUserRequest request) =>
        QueryAsync<UserSummary>(HttpMethod.Post, "api/admin/users", request);

    public Task<ApiResult> SetPasswordAsync(int userId, string password) =>
        CommandAsync(HttpMethod.Put, $"api/admin/users/{userId}/password", new PasswordRequest(password));

    public Task<ApiResult> SetAdminAsync(int userId, bool isAdmin) =>
        CommandAsync(HttpMethod.Put, $"api/admin/users/{userId}/role", new RoleRequest(isAdmin));

    public Task<ApiResult> SetDisabledAsync(int userId, bool disabled) =>
        CommandAsync(HttpMethod.Put, $"api/admin/users/{userId}/disabled", new DisabledRequest(disabled));

    public Task<ApiResult<MaintenanceStatus>> MaintenanceAsync() =>
        QueryAsync<MaintenanceStatus>(HttpMethod.Get, "api/admin/maintenance", null);

    public Task<ApiResult<MaintenanceStatus>> SetMaintenanceAsync(bool on) =>
        QueryAsync<MaintenanceStatus>(HttpMethod.Post, "api/admin/maintenance", new MaintenanceRequest(on));

    private async Task<ApiResult> CommandAsync(HttpMethod method, string path, object? body)
    {
        var (response, error) = await SendAsync(method, path, body);
        response?.Dispose();
        return new ApiResult(error);
    }

    private async Task<ApiResult<T>> QueryAsync<T>(HttpMethod method, string path, object? body)
    {
        var (response, error) = await SendAsync(method, path, body);
        if (response is null) return new ApiResult<T>(default, error);
        using (response)
        {
            try
            {
                return new ApiResult<T>(await response.Content.ReadFromJsonAsync<T>(), null);
            }
            catch (JsonException)
            {
                return new ApiResult<T>(default, Unexpected);
            }
        }
    }

    private async Task<(HttpResponseMessage? Response, string? Error)> SendAsync(HttpMethod method, string path, object? body)
    {
        if (session.IsEnded) return (null, SessionState.Ended);
        // Not disposed here: it would dispose the body too, and a test's handler reads the sent body after the call.
        var request = new HttpRequestMessage(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            // A timeout surfaces as a TaskCanceledException, which is an OperationCanceledException.
            return (null, NotReachable);
        }
        if (response.IsSuccessStatusCode) return (response, null);
        using (response)
        {
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                session.End();
                return (null, SessionState.Ended);
            }
            if (response.StatusCode == HttpStatusCode.Forbidden) return (null, Forbidden);
            return (null, await MessageOfAsync(response) ?? Unexpected);
        }
    }

    private static async Task<string?> MessageOfAsync(HttpResponseMessage response)
    {
        try
        {
            return (await response.Content.ReadFromJsonAsync<ErrorResponse>())?.Error;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
