namespace CromoBound.Client.Tests.Fakes;

/// <summary>An HTTP handler whose every request fails with the given exception, like a timeout or a dropped connection.</summary>
internal sealed class ThrowingHandler(Exception failure) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromException<HttpResponseMessage>(failure);
}
