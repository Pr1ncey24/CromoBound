using CromoBound.Data;

namespace CromoBound.Server.Matches;

/// <summary>Runs at startup, after the database is ready. Taking the card data loads it, so a data folder that can't be loaded stops
/// the server before it accepts requests.</summary>
internal sealed class MatchStartup(CardDatabase cards, ILogger<MatchStartup> log) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        log.LogInformation("Loaded {Count} cards.", cards.Cards.Count);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
