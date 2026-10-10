using CromoBound.Client.Services;
using Microsoft.AspNetCore.SignalR.Client;

namespace CromoBound.Client.Tests;

public class RetryPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 2)]
    [InlineData(2, 5)]
    [InlineData(3, 10)]
    [InlineData(4, 30)]
    [InlineData(500, 30)]
    public void Retries_wait_0_2_5_10_then_30_seconds_forever(long attempt, int seconds)
    {
        var delay = new ForeverRetryPolicy().NextRetryDelay(new RetryContext { PreviousRetryCount = attempt });

        Assert.Equal(TimeSpan.FromSeconds(seconds), delay);
    }
}
