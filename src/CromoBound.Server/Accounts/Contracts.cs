namespace CromoBound.Server.Accounts;

/// <summary>A sign-in by JSON; a form post uses the same two field names.</summary>
public sealed record LoginRequest(string? UserName, string? Password);
