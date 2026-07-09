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

    [Fact]
    public void Get_IdenticalRequest_ServedFromCache_HitsNetworkOnce()
    {
        var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        var http = new JsHttp(client, new Uri("https://example.test/"), NullLogger.Instance, CancellationToken.None);

        var first = http.request("/api/layout", "GET");
        var second = http.request("/api/layout", "GET");

        Assert.Equal(1, handler.SendCount);
        Assert.Same(first, second);
    }

    [Fact]
    public void Post_IsNotCached_HitsNetworkEachTime()
    {
        var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        var http = new JsHttp(client, new Uri("https://example.test/"), NullLogger.Instance, CancellationToken.None);

        http.request("/api/search", "POST", null, "{}");
        http.request("/api/search", "POST", null, "{}");

        Assert.Equal(2, handler.SendCount);
    }

    [Fact]
    public void Get_NextRouterPrefetch_IsSkipped_WithoutNetwork()
    {
        var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        var http = new JsHttp(client, new Uri("https://example.test/"), NullLogger.Instance, CancellationToken.None);

        var headers = JsonSerializer.Serialize(new Dictionary<string, string> { ["Next-Router-Prefetch"] = "1" });
        var response = http.request("/kontakt/?_rsc=1ot7s", "GET", headers);

        Assert.Equal(0, handler.SendCount);
        Assert.Equal(204, response.status);
        Assert.Null(response.error);
    }

    [Fact]
    public void Get_RequestNoStore_BypassesCache()
    {
        var handler = new CapturingHandler();
        using var client = new HttpClient(handler);
        var http = new JsHttp(client, new Uri("https://example.test/"), NullLogger.Instance, CancellationToken.None);

        var headers = JsonSerializer.Serialize(new Dictionary<string, string> { ["Cache-Control"] = "no-store" });
        http.request("/api/layout", "GET", headers);
        http.request("/api/layout", "GET", headers);

        Assert.Equal(2, handler.SendCount);
    }

    [Fact]
    public void Get_ResponseNoStore_IsNotCached()
    {
        var handler = new CapturingHandler { ResponseCacheControl = "no-store" };
        using var client = new HttpClient(handler);
        var http = new JsHttp(client, new Uri("https://example.test/"), NullLogger.Instance, CancellationToken.None);

        http.request("/api/layout", "GET");
        http.request("/api/layout", "GET");

        Assert.Equal(2, handler.SendCount);
    }

    [Fact]
    public void Get_ServerError_IsNotCached()
    {
        var handler = new CapturingHandler { StatusCode = HttpStatusCode.InternalServerError };
        using var client = new HttpClient(handler);
        var http = new JsHttp(client, new Uri("https://example.test/"), NullLogger.Instance, CancellationToken.None);

        http.request("/api/layout", "GET");
        http.request("/api/layout", "GET");

        Assert.Equal(2, handler.SendCount);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? ContentType { get; private set; }
        public int SendCount { get; private set; }
        public HttpStatusCode StatusCode { get; init; } = HttpStatusCode.OK;
        public string? ResponseCacheControl { get; init; }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;
            ContentType = request.Content?.Headers.ContentType?.MediaType;

            var response = new HttpResponseMessage(StatusCode) { Content = new StringContent("{}") };
            if (ResponseCacheControl is not null)
                response.Headers.TryAddWithoutValidation("Cache-Control", ResponseCacheControl);

            return response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(Send(request, cancellationToken));
    }
}
