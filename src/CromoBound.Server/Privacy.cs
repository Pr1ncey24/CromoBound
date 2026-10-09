namespace CromoBound.Server;

/// <summary>Only the owner's friends should know what the site is (spec §1): every response asks search engines not to index it.</summary>
internal static class Privacy
{
    public static Task NoIndexAsync(HttpContext context, RequestDelegate next)
    {
        context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        return next(context);
    }
}
