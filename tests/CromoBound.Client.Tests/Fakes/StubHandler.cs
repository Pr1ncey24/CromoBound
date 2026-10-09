using System.Net;
using System.Text;

namespace CromoBound.Client.Tests.Fakes;

/// <summary>An HTTP handler that answers every request with one status and body, and remembers the requests.</summary>
internal sealed class StubHandler(HttpStatusCode status, string body = "") : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}
