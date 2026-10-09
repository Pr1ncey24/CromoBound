namespace CromoBound.Contracts;

/// <summary>A user as admins see it: never a password hash, never a role name (only whether they manage users).</summary>
public sealed record UserSummary(int Id, string UserName, bool IsAdmin, bool Disabled);

public sealed record CreateUserRequest(string? UserName, string? Password, bool IsAdmin);

public sealed record PasswordRequest(string? Password);

public sealed record RoleRequest(bool? IsAdmin);

public sealed record DisabledRequest(bool? Disabled);

public sealed record MaintenanceRequest(bool? On);

/// <summary>Whether maintenance is on, and how many matches are still running.</summary>
public sealed record MaintenanceStatus(bool On, int RunningMatches);
