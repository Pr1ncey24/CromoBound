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
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapAccounts();
app.MapAdmin();
app.MapHub<GameHub>("/hub").RequireAuthorization(Policies.Seat);
app.Run();

/// <summary>The entry point; public so the test host can start the server.</summary>
public partial class Program;
