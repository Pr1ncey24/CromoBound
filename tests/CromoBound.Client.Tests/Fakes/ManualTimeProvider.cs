namespace CromoBound.Client.Tests.Fakes;

/// <summary>A clock whose timers fire only when the test says so.</summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    private readonly List<ManualTimer> _timers = [];

    public IReadOnlyList<TimeSpan> Periods => [.. _timers.Select(t => t.Period)];

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state, period);
        lock (_timers) _timers.Add(timer);
        return timer;
    }

    /// <summary>Fires every timer once.</summary>
    public void Tick()
    {
        List<ManualTimer> timers;
        lock (_timers) timers = [.. _timers];
        foreach (var timer in timers) timer.Fire();
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, TimeSpan period) : ITimer
    {
        public TimeSpan Period { get; private set; } = period;

        public void Fire() => callback(state);

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            Period = period;
            return true;
        }

        public void Dispose() { }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
