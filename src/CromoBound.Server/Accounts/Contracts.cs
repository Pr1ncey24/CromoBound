namespace CromoBound.Server.Accounts;

/// <summary>A sign-in by JSON; a form post uses the same two field names.</summary>
public sealed record LoginRequest(string? UserName, string? Password);

public sealed record ErrorResponse(string Error);

/// <summary>Who is signed in, and whether the UI should show user management (never a role name).</summary>
public sealed record MeResponse(string UserName, bool CanManageUsers);
