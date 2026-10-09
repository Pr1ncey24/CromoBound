using CromoBound.Server;
using CromoBound.Server.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCromoBoundStorage(builder.Configuration);

var app = builder.Build();
app.UseExceptionHandler(errors => errors.Run(ServerErrors.WriteGenericAsync));
app.Run();

/// <summary>The entry point; public so the test host can start the server.</summary>
public partial class Program;
