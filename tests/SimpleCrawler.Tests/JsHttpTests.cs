using System.Net;
using System.Text.Json;
using SimpleCrawler.Js.Network;
using Microsoft.Extensions.Logging.Abstractions;

namespace SimpleCrawler.Tests;

public class JsHttpTests
{
    [Fact]
    public void Post_CallerContentType_OverridesStringContentDefault()
    {
        var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        var http = new JsHttp(client, new Uri("https://example.test/"), NullLogger.Instance, CancellationToken.None);

        var headers = JsonSerializer.Serialize(new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        var response = http.request("/api/search/standard", "POST", headers, "{\"q\":\"drill\"}");

        Assert.Null(response.error);
        Assert.Equal("application/json", handler.ContentType);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? ContentType { get; private set; }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ContentType = request.Content?.Headers.ContentType?.MediaType;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") };
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));
    }
}
