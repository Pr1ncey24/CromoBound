using CromoBound.Contracts;

namespace CromoBound.Client.Services;

/// <summary>The server's HTTP endpoints (spec §5, §9). Every call answers with a result; none throws.</summary>
public interface IServerApi
{
    Task<ApiResult<MeResponse>> MeAsync();
    Task<ApiResult> LogoutAsync();
    Task<ApiResult<IReadOnlyList<UserSummary>>> UsersAsync();
    Task<ApiResult<UserSummary>> CreateUserAsync(CreateUserRequest request);
    Task<ApiResult> SetPasswordAsync(int userId, string password);
    Task<ApiResult> SetAdminAsync(int userId, bool isAdmin);
    Task<ApiResult> SetDisabledAsync(int userId, bool disabled);
    Task<ApiResult<MaintenanceStatus>> MaintenanceAsync();
    Task<ApiResult<MaintenanceStatus>> SetMaintenanceAsync(bool on);
}
