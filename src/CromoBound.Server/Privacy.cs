namespace CromoBound.Server;

/// <summary>Only the owner's friends should know what the site is (spec §1): every response asks search engines not to index it.
/// Every response also refuses content sniffing, framing and referrers, and API answers and the login page are never stored by
/// a cache. The headers are set as the response starts, so they also survive the error handler clearing the headers.</summary>
internal static class Privacy
{
    public static Task HeadersAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path;
        var noStore = path.StartsWithSegments("/api") || path.StartsWithSegments("/login");
        context.Response.OnStarting(static state =>
        {
            var (response, uncached) = ((HttpResponse, bool))state;
            var headers = response.Headers;
            headers["X-Robots-Tag"] = "noindex, nofollow";
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy = "frame-ancestors 'none'";
            if (uncached) headers.CacheControl = "no-store";
            return Task.CompletedTask;
        }, (context.Response, noStore));
        return next(context);
    }
}
