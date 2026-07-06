using Crawler.Core.Proxy;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace Crawler.Tests;

public class ProxyRoutingHandlerTests
{
    private static readonly ProxyInfo _proxy = new()
    {
        Host = "proxy",
        Port = 1080,
        Protocol = ProxyProtocol.Http,
    };

    private static HttpClient BuildClient(IProxyPool pool, StubHandler inner, ProxyPoolOptions? options = null)
    {
        var handler = new ProxyRoutingHandler(
            pool,
            new FakeClientProvider(inner),
            options ?? new ProxyPoolOptions { MaxRetries = 3 },
            NullLogger<ProxyRoutingHandler>.Instance);

        return new HttpClient(handler, disposeHandler: true);
    }

    [Fact]
    public async Task Success_Reports_Success_And_Does_Not_Retry()
    {
        var spy = new SpyProxyPool { AcquireResult = _proxy };
        var stub = new StubHandler();

        using var client = BuildClient(spy, stub);
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");
        using var response = await client.SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, stub.SendCount);
        Assert.Single(spy.Successes);
        Assert.Empty(spy.Failures);
    }

    [Fact]
    public async Task Retryable_Failure_Rotates_And_Retries()
    {
        var spy = new SpyProxyPool { AcquireResult = _proxy };
        var stub = new StubHandler();
        stub.Enqueue(HttpStatusCode.ServiceUnavailable);

        using var client = BuildClient(spy, stub);
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");
        using var response = await client.SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.SendCount);
        Assert.Single(spy.Failures);
        Assert.Equal(ProxyFailureKind.Http5xx, spy.Failures[0].Kind);
        Assert.Single(spy.Successes);
    }

    [Fact]
    public async Task NonRetryable_Response_Is_Returned_Without_Burning_Proxy()
    {
        var spy = new SpyProxyPool { AcquireResult = _proxy };
        var stub = new StubHandler();
        stub.Enqueue(HttpStatusCode.NotFound);

        using var client = BuildClient(spy, stub);
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");
        using var response = await client.SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(1, stub.SendCount);
        Assert.Single(spy.Successes);
        Assert.Empty(spy.Failures);
    }

    [Fact]
    public async Task Exhausted_Pool_Throws_Before_Sending()
    {
        var spy = new SpyProxyPool { AcquireResult = null };
        var stub = new StubHandler();

        using var client = BuildClient(spy, stub);
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");

        await Assert.ThrowsAsync<ProxyPoolExhaustedException>(
            async () => await client.SendAsync(req, CancellationToken.None));

        Assert.Equal(0, stub.SendCount);
    }

    [Fact]
    public async Task All_Attempts_Failing_Throws_Aggregate_HttpRequestException()
    {
        var spy = new SpyProxyPool { AcquireResult = _proxy };
        var stub = new StubHandler { ThrowAlways = new HttpRequestException("boom") };

        using var client = BuildClient(spy, stub, new ProxyPoolOptions { MaxRetries = 2 });
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");

        await Assert.ThrowsAsync<HttpRequestException>(
            async () => await client.SendAsync(req, CancellationToken.None));

        Assert.Equal(3, stub.SendCount);
        Assert.Equal(3, spy.Failures.Count);
    }

    [Fact]
    public async Task Caller_Cancellation_Propagates_Without_Burning_Proxy()
    {
        var spy = new SpyProxyPool { AcquireResult = _proxy };
        var stub = new StubHandler { ThrowAlways = new OperationCanceledException() };

        using var client = BuildClient(spy, stub);
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await client.SendAsync(req, cts.Token));

        Assert.Empty(spy.Failures);
    }

    private sealed class SpyProxyPool : IProxyPool
    {
        public ProxyInfo? AcquireResult { get; set; }
        public List<ProxyInfo> Successes { get; } = [];
        public List<(ProxyInfo Proxy, ProxyFailureKind Kind)> Failures { get; } = [];

        public ProxyInfo? Acquire() => AcquireResult;

        public void ReportSuccess(ProxyInfo proxy) => Successes.Add(proxy);

        public void ReportFailure(ProxyInfo proxy, ProxyFailureKind kind) => Failures.Add((proxy, kind));

        public ProxyPoolSnapshot Snapshot() => new() { Total = 1, Healthy = 1 };
    }

    private sealed class FakeClientProvider : IProxyClientProvider
    {
        private readonly HttpClient _client;

        public FakeClientProvider(HttpMessageHandler handler)
        {
            _client = new HttpClient(handler, disposeHandler: false);
        }

        public HttpClient ClientFor(ProxyInfo proxy) => _client;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<object> _script = new();

        public int SendCount { get; private set; }

        public Exception? ThrowAlways { get; set; }

        public void Enqueue(HttpStatusCode status) => _script.Enqueue(status);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;

            if (ThrowAlways is not null)
                return Task.FromException<HttpResponseMessage>(ThrowAlways);

            if (_script.Count > 0)
            {
                var next = _script.Dequeue();
                if (next is Exception ex)
                    return Task.FromException<HttpResponseMessage>(ex);
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)next));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
