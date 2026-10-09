namespace CromoBound.Client.Services;

/// <summary>A command's outcome: success, or the message to show.</summary>
public sealed record ApiResult(string? Error)
{
    public static ApiResult Success { get; } = new((string?)null);

    public bool Ok => Error is null;
}

/// <summary>A query's outcome: the value, or the message to show.</summary>
public sealed record ApiResult<T>(T? Value, string? Error)
{
    public bool Ok => Error is null;
}
