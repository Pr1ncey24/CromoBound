using Microsoft.AspNetCore.SignalR.Client;

namespace CromoBound.Client.Services;

/// <summary>Reconnect after 0, 2, 5 and 10 seconds, then every 30 seconds for as long as the tab is open (spec §5.2).</summary>
public sealed class ForeverRetryPolicy : IRetryPolicy
{
    private static readonly TimeSpan[] First = [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)];
    private static readonly TimeSpan Then = TimeSpan.FromSeconds(30);

    public TimeSpan? NextRetryDelay(RetryContext retryContext) =>
        retryContext.PreviousRetryCount < First.Length ? First[retryContext.PreviousRetryCount] : Then;
}
