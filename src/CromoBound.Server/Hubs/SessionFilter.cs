using CromoBound.Server.Accounts;
using CromoBound.Server.Storage;
using Microsoft.AspNetCore.SignalR;

namespace CromoBound.Server.Hubs;

/// <summary>Before every hub call: the caller's session must still be current. A stale one is refused and its connection closed.</summary>
internal sealed class SessionFilter : IHubFilter
{
    public const string Ended = "Your session has ended.";

    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocation, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var db = invocation.ServiceProvider.GetRequiredService<CromoDbContext>();
        if (await Sessions.IsCurrentAsync(invocation.Context.User!, db)) return await next(invocation);
        invocation.Context.Abort();
        throw new HubException(Ended);
    }
}
