using CromoBound.Data;
using CromoBound.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;

namespace CromoBound.Server.Matches;

internal static class MatchesSetup
{
    /// <summary>The card data (loaded once, shared read-only), the match store, the lobby, the hub and the startup work. Call after
    /// <c>AddCromoBoundStorage</c>, so the database is ready before <see cref="MatchStartup"/> runs.</summary>
    public static IServiceCollection AddCromoBoundMatches(this IServiceCollection services)
    {
        services.AddSingleton(provider => LoadCards(provider.GetRequiredService<IOptions<ServerOptions>>().Value.DataFolder));
        services.AddSingleton<IMatchStore, MatchStore>();
        services.AddHostedService<MatchStartup>();
        services.AddSingleton<Lobby>();
        services.AddSignalR(hub =>
        {
            hub.EnableDetailedErrors = false;
            hub.AddFilter<SessionFilter>();
        }).AddJsonProtocol(json => json.PayloadSerializerOptions = ServerJson.Options);
        return services;
    }

    private static CardDatabase LoadCards(string folder)
    {
        try
        {
            return CardRepository.Load(folder);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"{ServerOptions.Section}:{nameof(ServerOptions.DataFolder)} must name the card data folder: '{folder}' can't be loaded ({ex.Message})", ex);
        }
    }
}
