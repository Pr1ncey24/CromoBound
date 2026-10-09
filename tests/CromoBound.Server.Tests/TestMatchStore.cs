using CromoBound.Engine.Matches;
using CromoBound.Server.Matches;
using CromoBound.Server.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CromoBound.Server.Tests;

/// <summary>The real match store, counting saves and failing them on demand.</summary>
internal sealed class TestMatchStore(MatchStore inner) : IMatchStore
{
    private int _saves;
    private volatile bool _failSaves;

    /// <summary>Puts one of these in place of the server's store: <c>new ServerFactory(services: TestMatchStore.Register)</c>.</summary>
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<MatchStore>();
        services.AddSingleton<TestMatchStore>();
        services.AddSingleton<IMatchStore>(provider => provider.GetRequiredService<TestMatchStore>());
    }

    /// <summary>Saves that went through.</summary>
    public int Saves => Volatile.Read(ref _saves);

    public bool FailSaves
    {
        get => _failSaves;
        set => _failSaves = value;
    }

    public Task CreateAsync(Guid id, int seat0UserId, int seat1UserId, MatchRecord record) => inner.CreateAsync(id, seat0UserId, seat1UserId, record);

    public Task SaveAsync(Guid id, MatchRecord record, MatchStatus status)
    {
        if (FailSaves) throw new IOException("The disk is full.");
        Interlocked.Increment(ref _saves);
        return inner.SaveAsync(id, record, status);
    }

    public Task SetStatusAsync(Guid id, MatchStatus status) => inner.SetStatusAsync(id, status);

    public Task<IReadOnlyList<MatchEntity>> RunningAsync() => inner.RunningAsync();
}
