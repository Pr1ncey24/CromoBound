namespace CromoBound.Client.Services;

/// <summary>Renews the session while the app is open (spec §5.1): asks who is signed in every 30 minutes, which slides the cookie.
/// The hub's own traffic doesn't renew it. A ping that finds the session over ends it, through the API.</summary>
public sealed class SessionKeeper(IServerApi api, TimeProvider time) : IAsyncDisposable
{
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private CancellationTokenSource? _stop;

    /// <summary>Starts the pings; a second call does nothing.</summary>
    public void Start()
    {
        if (_stop is not null) return;
        _stop = new CancellationTokenSource();
        _ = RunAsync(_stop.Token);
    }

    public async Task PingAsync() => await api.MeAsync();

    private async Task RunAsync(CancellationToken cancel)
    {
        using var timer = new PeriodicTimer(Interval, time);
        try
        {
            while (await timer.WaitForNextTickAsync(cancel)) await PingAsync();
        }
        catch (OperationCanceledException)
        {
        }
    }

    public ValueTask DisposeAsync()
    {
        _stop?.Cancel();
        _stop?.Dispose();
        return ValueTask.CompletedTask;
    }
}
