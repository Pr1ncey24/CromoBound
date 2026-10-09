using System.Text.Encodings.Web;

namespace CromoBound.Server.Accounts;

/// <summary>The two server-made pages. The login page names nothing about the site (spec §1); the home page is a placeholder
/// until the Phase 4 UI.</summary>
internal static class Pages
{
    public static string Login(bool failed) => $$"""
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex, nofollow">
        <title>Sign in</title>
        <style>
        body { font-family: system-ui, sans-serif; display: grid; place-items: center; min-height: 100vh; margin: 0; }
        form { display: grid; gap: 0.75rem; width: min(20rem, 90vw); }
        input, button { font: inherit; padding: 0.5rem; }
        </style>
        </head>
        <body>
        <main>
        <h1>Sign in</h1>
        {{(failed ? $"<p role=\"alert\">{LoginEndpoints.Failure}</p>" : "")}}
        <form method="post" action="/login">
        <label>Username <input name="userName" autocomplete="username" required></label>
        <label>Password <input name="password" type="password" autocomplete="current-password" required></label>
        <button type="submit">Sign in</button>
        </form>
        </main>
        </body>
        </html>
        """;

    public static string Home(string userName) => $$"""
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width, initial-scale=1">
        <meta name="robots" content="noindex, nofollow">
        <title>CromoBound</title>
        </head>
        <body>
        <main>
        <p>Signed in as {{HtmlEncoder.Default.Encode(userName)}}.</p>
        <form method="post" action="/logout"><button type="submit">Sign out</button></form>
        </main>
        </body>
        </html>
        """;
}
