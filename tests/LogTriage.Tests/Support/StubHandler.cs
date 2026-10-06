using System.Net;
using System.Text;

namespace LogTriage.Tests.Support;

internal sealed record RecordedRequest(HttpMethod Method, Uri Uri, string Body, string? Authorization);

/// <summary>An HTTP handler that answers from a function and remembers what it was asked.</summary>
internal sealed class StubHandler(Func<RecordedRequest, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<RecordedRequest> Requests { get; } = [];

    public static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(request.Method, request.RequestUri!, body, request.Headers.Authorization?.ToString());
        Requests.Add(recorded);
        return respond(recorded);
    }
}

internal sealed class StubClientFactory(HttpMessageHandler handler, string baseUrl) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri(baseUrl) };
}
