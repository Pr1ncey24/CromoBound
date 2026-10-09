namespace CromoBound.Server.Accounts;

/// <summary>A user as admins see it: never a password hash, never a role name (only whether they manage users).</summary>
public sealed record UserSummary(int Id, string UserName, bool IsAdmin, bool Disabled);

public sealed record CreateUserRequest(string? UserName, string? Password, bool IsAdmin);

public sealed record PasswordRequest(string? Password);

public sealed record RoleRequest(bool IsAdmin);

public sealed record DisabledRequest(bool Disabled);
