using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Retry;

namespace SimpleCrawler.Tests;

public class RetryExecutorTests
{
    private static RetryOptions Options(int maxRetries = 2, bool delayOnRateLimit = true, TimeSpan? attemptTimeout = null) => new()
    {
        MaxRetries = maxRetries,
        BaseDelay = TimeSpan.Zero,
        MaxDelay = TimeSpan.Zero,
        DelayOnRateLimit = delayOnRateLimit,
        AttemptTimeout = attemptTimeout ?? Timeout.InfiniteTimeSpan,
    };

    private static Func<TimeSpan, CancellationToken, Task> Counting(Action onDelay) =>
        (_, _) => { onDelay(); return Task.CompletedTask; };

    [Fact]
    public async Task NoPool_Runs_All_Attempts_And_Returns_OnExhausted()
    {
        var delays = 0;
        var executor = new RetryExecutor(Options(maxRetries: 2), pool: null, delay: Counting(() => delays++));

        var attempts = 0;
        var result = await executor.ExecuteAsync(
            (_, _) => { attempts++; return Task.FromResult(RetryAttempt<int>.Failed(RetryReason.ServerError)); },
            () => -1,
            CancellationToken.None);

        Assert.Equal(-1, result);
        Assert.Equal(3, attempts);
        Assert.Equal(2, delays);
    }

    [Fact]
    public async Task NoPool_Returns_Value_On_Success_Without_Delay()
    {
        var delays = 0;
        var executor = new RetryExecutor(Options(), pool: null, delay: Counting(() => delays++));

        var result = await executor.ExecuteAsync(
            (_, _) => Task.FromResult(RetryAttempt<int>.Ok(42)),
            () => -1,
            CancellationToken.None);

        Assert.Equal(42, result);
        Assert.Equal(0, delays);
    }

    [Fact]
    public async Task Multi_Proxy_Rotation_Does_Not_Delay()
    {
        var delays = 0;
        var pool = new FakePool(proxyCount: 3);
        var executor = new RetryExecutor(Options(maxRetries: 2), pool, delay: Counting(() => delays++));

        await executor.ExecuteAsync(
            (_, _) => Task.FromResult(RetryAttempt<int>.Failed(RetryReason.ServerError)),
            () => -1,
            CancellationToken.None);

        Assert.Equal(0, delays);
        Assert.Equal(3, pool.Failures.Count);
    }

    [Fact]
    public async Task Multi_Proxy_RateLimited_Delays_When_Enabled()
    {
        var delays = 0;
        var pool = new FakePool(proxyCount: 3);
        var executor = new RetryExecutor(Options(maxRetries: 2, delayOnRateLimit: true), pool, delay: Counting(() => delays++));

        await executor.ExecuteAsync(
            (_, _) => Task.FromResult(RetryAttempt<int>.Failed(RetryReason.RateLimited)),
            () => -1,
            CancellationToken.None);

        Assert.Equal(2, delays);
    }

    [Fact]
    public async Task Single_Proxy_Pool_Backs_Off()
    {
        var delays = 0;
        var pool = new FakePool(proxyCount: 1);
        var executor = new RetryExecutor(Options(maxRetries: 2), pool, delay: Counting(() => delays++));

        await executor.ExecuteAsync(
            (_, _) => Task.FromResult(RetryAttempt<int>.Failed(RetryReason.ServerError)),
            () => -1,
            CancellationToken.None);

        Assert.Equal(2, delays);
    }

    [Fact]
    public async Task Empty_Pool_Throws_In_ExecuteAsync()
    {
        var pool = new FakePool(proxyCount: 2, acquireNull: true);
        var executor = new RetryExecutor(Options(), pool);

        var attempts = 0;
        await Assert.ThrowsAsync<ProxyPoolExhaustedException>(() => executor.ExecuteAsync(
            (_, _) => { attempts++; return Task.FromResult(RetryAttempt<int>.Ok(1)); },
            () => -1,
            CancellationToken.None));

        Assert.Equal(0, attempts);
    }

    [Fact]
    public async Task Empty_Pool_Falls_Back_To_Direct_Attempt_In_DirectFallback()
    {
        var pool = new FakePool(proxyCount: 2, acquireNull: true);
        var executor = new RetryExecutor(Options(), pool);

        ProxyInfo? seenProxy = null;
        var attempts = 0;
        var result = await executor.ExecuteWithDirectFallbackAsync(
            (proxy, _) => { attempts++; seenProxy = proxy; return Task.FromResult(RetryAttempt<int>.Ok(7)); },
            () => -1,
            CancellationToken.None);

        Assert.Equal(7, result);
        Assert.Equal(1, attempts);
        Assert.Null(seenProxy);
    }

    [Fact]
    public async Task Cancellation_Propagates_Without_Reporting_Failure()
    {
        var pool = new FakePool(proxyCount: 2);
        var executor = new RetryExecutor(Options(), pool);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var attempts = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(
            (_, _) => { attempts++; return Task.FromResult(RetryAttempt<int>.Ok(1)); },
            () => -1,
            cts.Token));

        Assert.Equal(0, attempts);
        Assert.Empty(pool.Failures);
    }

    [Fact]
    public async Task Attempt_Timeout_Is_Classified_As_Timeout_And_Retried()
    {
        var executor = new RetryExecutor(
            Options(maxRetries: 2, attemptTimeout: TimeSpan.FromMilliseconds(100)),
            pool: null);

        var calls = 0;
        var result = await executor.ExecuteAsync(
            async (_, token) =>
            {
                calls++;
                if (calls == 1)
                    await Task.Delay(Timeout.Infinite, token);
                return RetryAttempt<int>.Ok(99);
            },
            () => -1,
            TestContext.Current.CancellationToken);

        Assert.Equal(99, result);
        Assert.Equal(2, calls);
    }

    private sealed class FakePool : IProxyPool
    {
        private readonly ProxyInfo[] _proxies;
        private readonly bool _acquireNull;
        private int _cursor;

        public FakePool(int proxyCount, bool acquireNull = false)
        {
            _proxies = Enumerable.Range(0, proxyCount)
                .Select(i => new ProxyInfo { Host = $"p{i}", Port = 1080, Protocol = ProxyProtocol.Http })
                .ToArray();
            _acquireNull = acquireNull;
        }

        public List<ProxyInfo> Successes { get; } = [];
        public List<(ProxyInfo Proxy, RetryReason Reason)> Failures { get; } = [];

        public IReadOnlyList<ProxyInfo> Proxies => _proxies;

        public ProxyInfo? Acquire() => _acquireNull ? null : _proxies[_cursor++ % _proxies.Length];

        public void ReportSuccess(ProxyInfo proxy) => Successes.Add(proxy);

        public void ReportFailure(ProxyInfo proxy, RetryReason reason) => Failures.Add((proxy, reason));

        public ProxyPoolSnapshot Snapshot() => new() { Total = _proxies.Length, Healthy = _proxies.Length };
    }
}
