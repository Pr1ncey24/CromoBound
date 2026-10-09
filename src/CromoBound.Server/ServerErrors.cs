namespace CromoBound.Server;

/// <summary>Unexpected errors (spec §4.5): a generic 500 with no details; the exception itself goes to the log.</summary>
internal static class ServerErrors
{
    public const string Generic = "Something went wrong.";

    public static Task WriteGenericAsync(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(Generic);
    }
}
