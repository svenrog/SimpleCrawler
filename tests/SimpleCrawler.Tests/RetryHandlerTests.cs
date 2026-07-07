using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Retry;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace SimpleCrawler.Tests;

public class RetryHandlerTests
{
    private static readonly ProxyInfo _proxy = new()
    {
        Host = "proxy",
        Port = 1080,
        Protocol = ProxyProtocol.Http,
    };

    // Zero delays keep retry tests instant; the executor's backoff is covered separately.
    private static RetryOptions FastOptions(int maxRetries = 3, TimeSpan? attemptTimeout = null) => new()
    {
        MaxRetries = maxRetries,
        BaseDelay = TimeSpan.Zero,
        MaxDelay = TimeSpan.Zero,
        AttemptTimeout = attemptTimeout ?? Timeout.InfiniteTimeSpan,
    };

    private static HttpClient BuildProxyClient(IProxyPool pool, StubHandler inner, RetryOptions? options = null)
    {
        var handler = new RetryHandler(
            options ?? FastOptions(),
            new FakeClientProvider(inner),
            pool,
            directInner: null,
            NullLogger<RetryHandler>.Instance);

        return new HttpClient(handler, disposeHandler: true);
    }

    private static HttpClient BuildDirectClient(StubHandler inner, RetryOptions? options = null)
    {
        var handler = new RetryHandler(
            options ?? FastOptions(),
            clients: null,
            pool: null,
            directInner: inner,
            NullLogger<RetryHandler>.Instance);

        return new HttpClient(handler, disposeHandler: true);
    }

    [Fact]
    public async Task Success_Reports_Success_And_Does_Not_Retry()
    {
        var spy = new SpyProxyPool { AcquireResult = _proxy };
        var stub = new StubHandler();

        using var client = BuildProxyClient(spy, stub);
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

        using var client = BuildProxyClient(spy, stub);
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");
        using var response = await client.SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.SendCount);
        Assert.Single(spy.Failures);
        Assert.Equal(RetryReason.ServerError, spy.Failures[0].Reason);
        Assert.Single(spy.Successes);
    }

    [Fact]
    public async Task NonRetryable_Response_Is_Returned_Without_Burning_Proxy()
    {
        var spy = new SpyProxyPool { AcquireResult = _proxy };
        var stub = new StubHandler();
        stub.Enqueue(HttpStatusCode.NotFound);

        using var client = BuildProxyClient(spy, stub);
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

        using var client = BuildProxyClient(spy, stub);
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

        using var client = BuildProxyClient(spy, stub, FastOptions(maxRetries: 2));
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

        using var client = BuildProxyClient(spy, stub);
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await client.SendAsync(req, cts.Token));

        Assert.Empty(spy.Failures);
    }

    [Fact]
    public async Task No_Proxy_Retries_Transient_Failure_To_Success()
    {
        var stub = new StubHandler();
        stub.Enqueue(HttpStatusCode.ServiceUnavailable);

        using var client = BuildDirectClient(stub);
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");
        using var response = await client.SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.SendCount);
    }

    [Fact]
    public async Task No_Proxy_Exhausting_Retries_Returns_Last_Response_Without_Throwing()
    {
        var stub = new StubHandler();
        stub.Enqueue(HttpStatusCode.ServiceUnavailable);
        stub.Enqueue(HttpStatusCode.ServiceUnavailable);
        stub.Enqueue(HttpStatusCode.ServiceUnavailable);

        using var client = BuildDirectClient(stub, FastOptions(maxRetries: 2));
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");
        using var response = await client.SendAsync(req, CancellationToken.None);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(3, stub.SendCount);
    }

    [Fact]
    public async Task No_Proxy_Connection_Failure_Exhaustion_Throws_HttpRequestException()
    {
        var stub = new StubHandler { ThrowAlways = new HttpRequestException("down") };

        using var client = BuildDirectClient(stub, FastOptions(maxRetries: 2));
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");

        await Assert.ThrowsAsync<HttpRequestException>(
            async () => await client.SendAsync(req, CancellationToken.None));

        Assert.Equal(3, stub.SendCount);
    }

    [Fact]
    public void Synchronous_Send_Retries_Transient_Failure_To_Success()
    {
        var stub = new StubHandler();
        stub.Enqueue(HttpStatusCode.ServiceUnavailable);

        using var client = BuildDirectClient(stub);
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");
        using var response = client.Send(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.SendCount);
    }

    [Fact]
    public async Task Attempt_Timeout_Cancels_Slow_Attempt_And_Retries()
    {
        var stub = new StubHandler { HangCount = 1 };
        stub.Enqueue(HttpStatusCode.OK);

        using var client = BuildDirectClient(stub, FastOptions(maxRetries: 2, attemptTimeout: TimeSpan.FromMilliseconds(100)));
        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.com/");
        using var response = await client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, stub.SendCount);
    }

    private sealed class SpyProxyPool : IProxyPool
    {
        public ProxyInfo? AcquireResult { get; set; }
        public List<ProxyInfo> Successes { get; } = [];
        public List<(ProxyInfo Proxy, RetryReason Reason)> Failures { get; } = [];

        public IReadOnlyList<ProxyInfo> Proxies => AcquireResult is null ? [] : [AcquireResult];

        public ProxyInfo? Acquire() => AcquireResult;

        public void ReportSuccess(ProxyInfo proxy) => Successes.Add(proxy);

        public void ReportFailure(ProxyInfo proxy, RetryReason reason) => Failures.Add((proxy, reason));

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
        private readonly Queue<HttpStatusCode> _script = new();

        public int SendCount { get; private set; }

        public Exception? ThrowAlways { get; set; }

        public int HangCount { get; set; }

        public void Enqueue(HttpStatusCode status) => _script.Enqueue(status);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;

            if (HangCount > 0)
            {
                HangCount--;
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            if (ThrowAlways is not null)
                throw ThrowAlways;

            if (_script.Count > 0)
                return new HttpResponseMessage(_script.Dequeue());

            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SendCount++;

            if (ThrowAlways is not null)
                throw ThrowAlways;

            if (_script.Count > 0)
                return new HttpResponseMessage(_script.Dequeue());

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
