using CromoBound.Server;
using CromoBound.Server.Accounts;
using CromoBound.Server.Hubs;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);
// Authorization failures name the roles that were missing; they are logged only from Warning up, so role identifiers never
// reach the log (spec §4.2) even when Microsoft.AspNetCore is set to log more.
builder.Logging.AddFilter("Microsoft.AspNetCore.Authorization", LogLevel.Warning);
builder.Services.AddCromoBoundStorage(builder.Configuration);
builder.Services.AddCromoBoundAccounts(builder.Configuration);
builder.Services.AddCromoBoundProxies();
builder.Services.AddCromoBoundMatches();
// A body that can't be read is a plain 400 in every environment, never an exception (a 500).
builder.Services.Configure<RouteHandlerOptions>(handlers => handlers.ThrowOnBadRequest = false);

var app = builder.Build();
app.UseForwardedHeaders();
app.Use(Privacy.HeadersAsync);
app.UseExceptionHandler(errors => errors.Run(ServerErrors.WriteGenericAsync));
// The app's routes are served its index.html (behind the player policy, like all of its files).
app.Use(ClientApp.RewriteAsync);
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapAccounts();
app.MapAdmin();
app.MapMaintenance();
// A hub connection can outlive the sign-in cookie, so it is closed when the cookie expires.
app.MapHub<GameHub>("/hub", options => options.CloseOnAuthenticationExpiration = true).RequireAuthorization(Policies.Seat);
// The Blazor app's files, as endpoints, so the fallback policy keeps them behind sign-in.
app.MapStaticAssets();
app.Run();

/// <summary>The entry point; public so the test host can start the server.</summary>
public partial class Program;
