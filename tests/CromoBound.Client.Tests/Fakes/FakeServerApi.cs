using CromoBound.Client.Services;
using CromoBound.Contracts;

namespace CromoBound.Client.Tests.Fakes;

/// <summary>The server's HTTP side, in memory. A test sets what it answers and reads what was asked.</summary>
internal sealed class FakeServerApi(SessionState session) : IServerApi
{
    public MeResponse Me { get; set; } = new("marco", false);

    /// <summary>When set, every call behaves like a 401: the session ends.</summary>
    public bool SessionOver { get; set; }

    /// <summary>When set, the next command fails with this message (once).</summary>
    public string? NextError { get; set; }

    /// <summary>When set, the next query (a read) fails with this message (once).</summary>
    public string? NextQueryError { get; set; }

    /// <summary>When set, the next <c>MeAsync</c> throws (once), like a request that timed out.</summary>
    public bool FailNextMe { get; set; }

    public List<UserSummary> Users { get; } = [];
    public MaintenanceStatus Maintenance { get; set; } = new(false, 0);
    public List<string> Calls { get; } = [];
    public int MeCalls { get; private set; }

    public Task<ApiResult<MeResponse>> MeAsync()
    {
        var fail = FailNextMe;
        FailNextMe = false;
        MeCalls++;
        if (fail) throw new TaskCanceledException("The request timed out.");
        return Task.FromResult(Answer(() => Me));
    }

    public Task<ApiResult> LogoutAsync() => Task.FromResult(Command("logout"));

    public Task<ApiResult<IReadOnlyList<UserSummary>>> UsersAsync() =>
        Task.FromResult(Answer<IReadOnlyList<UserSummary>>(() => [.. Users]));

    public Task<ApiResult<UserSummary>> CreateUserAsync(CreateUserRequest request)
    {
        var result = Command($"create {request.UserName} {request.IsAdmin}");
        if (!result.Ok) return Task.FromResult(new ApiResult<UserSummary>(default, result.Error));
        var user = new UserSummary(Users.Count + 1, request.UserName!, request.IsAdmin, false);
        Users.Add(user);
        return Task.FromResult(new ApiResult<UserSummary>(user, null));
    }

    public Task<ApiResult> SetPasswordAsync(int id, string password) => Task.FromResult(Command($"password {id}"));

    public Task<ApiResult> SetAdminAsync(int id, bool isAdmin) => Task.FromResult(Command($"admin {id} {isAdmin}", () =>
        Users[Users.FindIndex(u => u.Id == id)] = Users.Single(u => u.Id == id) with { IsAdmin = isAdmin }));

    public Task<ApiResult> SetDisabledAsync(int id, bool disabled) => Task.FromResult(Command($"disabled {id} {disabled}", () =>
        Users[Users.FindIndex(u => u.Id == id)] = Users.Single(u => u.Id == id) with { Disabled = disabled }));

    public Task<ApiResult<MaintenanceStatus>> MaintenanceAsync() => Task.FromResult(Answer(() => Maintenance));

    public Task<ApiResult<MaintenanceStatus>> SetMaintenanceAsync(bool on)
    {
        var result = Command($"maintenance {on}", () => Maintenance = Maintenance with { On = on });
        return Task.FromResult(result.Ok ? new ApiResult<MaintenanceStatus>(Maintenance, null) : new ApiResult<MaintenanceStatus>(default, result.Error));
    }

    private ApiResult<T> Answer<T>(Func<T> value)
    {
        if (SessionOver)
        {
            session.End();
            return new ApiResult<T>(default, SessionState.Ended);
        }
        if (NextQueryError is { } error)
        {
            NextQueryError = null;
            return new ApiResult<T>(default, error);
        }
        return new ApiResult<T>(value(), null);
    }

    private ApiResult Command(string call, Action? apply = null)
    {
        Calls.Add(call);
        if (SessionOver)
        {
            session.End();
            return new ApiResult(SessionState.Ended);
        }
        if (NextError is { } error)
        {
            NextError = null;
            return new ApiResult(error);
        }
        apply?.Invoke();
        return ApiResult.Success;
    }
}
