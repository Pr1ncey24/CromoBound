namespace CromoBound.Server;

/// <summary>Only the owner's friends should know what the site is (spec §1): every response asks search engines not to index it.
/// The header is set as the response starts, so it also survives the error handler clearing the headers.</summary>
internal static class Privacy
{
    public static Task NoIndexAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.OnStarting(static state =>
        {
            ((HttpContext)state).Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            return Task.CompletedTask;
        }, context);
        return next(context);
    }
}
