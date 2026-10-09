using CromoBound.Server;
using CromoBound.Server.Accounts;
using CromoBound.Server.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(kestrel => kestrel.AddServerHeader = false);
builder.Services.AddCromoBoundStorage(builder.Configuration);
builder.Services.AddCromoBoundAccounts(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler(errors => errors.Run(ServerErrors.WriteGenericAsync));
app.Use(Privacy.NoIndexAsync);
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapAccounts();
app.Run();

/// <summary>The entry point; public so the test host can start the server.</summary>
public partial class Program;
