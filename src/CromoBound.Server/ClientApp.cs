namespace CromoBound.Server;

/// <summary>The Blazor app's routes (spec §4). A request for one is served the app's <c>index.html</c>, by rewriting the path before
/// routing to the static-asset endpoint that serves it. That endpoint, like every file of the app, is behind the player policy, and a
/// path that isn't listed here stays a 404. Each page Plan J adds is added here.</summary>
internal static class ClientApp
{
    public const string Page = "/index.html";

    private static readonly string[] Routes = ["/", "/decks", "/admin/users", "/admin/maintenance"];

    private const string MatchPrefix = "/match/";

    public static bool IsAppRoute(PathString path)
    {
        var value = path.Value ?? "";
        if (Routes.Any(route => string.Equals(route, value, StringComparison.OrdinalIgnoreCase))) return true;
        return value.StartsWith(MatchPrefix, StringComparison.OrdinalIgnoreCase) && Guid.TryParseExact(value[MatchPrefix.Length..], "D", out _);
    }

    public static Task RewriteAsync(HttpContext context, RequestDelegate next)
    {
        if (HttpMethods.IsGet(context.Request.Method) && IsAppRoute(context.Request.Path)) context.Request.Path = Page;
        return next(context);
    }
}
