using CromoBound.Server.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CromoBound.Server.Storage;

internal static class StorageSetup
{
    /// <summary>Settings, the database, the clock, password hashing, the user store, and the startup setup.</summary>
    public static IServiceCollection AddCromoBoundStorage(this IServiceCollection services, IConfiguration configuration)
    {
        ServerOptions.AddTo(services, configuration);
        services.AddDbContext<CromoDbContext>((provider, db) =>
            db.UseSqlite($"Data Source={provider.GetRequiredService<IOptions<ServerOptions>>().Value.DatabasePath}"));
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasher<UserEntity>, PasswordHasher<UserEntity>>();
        services.AddScoped<UserStore>();
        services.AddHostedService<DatabaseSetup>();
        return services;
    }
}
