using SimpleCrawler.Core.Proxy;

namespace SimpleCrawler.Core.Retry;

// Single home for the acquire/report/retry loop shared by every fetch path (static handler,
// headless crawlers, robots probes). A proxy pool is optional: with one, attempts rotate through
// proxies and report health; without one, attempts still retry transient failures with backoff.
// Callers supply one classified attempt; this decides rotation, backoff, timeout and exhaustion so
// that logic lives in exactly one place.
public sealed class RetryExecutor
{
    private readonly RetryOptions _options;
    private readonly IProxyPool? _pool;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public RetryExecutor(RetryOptions options, IProxyPool? pool = null, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _options = options;
        _pool = pool;
        _delay = delay ?? Task.Delay;
    }

    // Retries until one attempt succeeds. With a pool, exhausting it (below cutoff) aborts the crawl;
    // exhausting the retry budget yields onExhausted().
    public Task<T> ExecuteAsync<T>(
        Func<ProxyInfo?, CancellationToken, Task<RetryAttempt<T>>> attempt,
        Func<T> onExhausted,
        CancellationToken cancellationToken)
        => RunAsync(attempt, onExhausted, directFallbackOnEmpty: false, cancellationToken);

    // As ExecuteAsync, but an empty pool falls back to a direct (un-proxied) attempt instead of
    // aborting - used for robots/sitemap probes, which must not fail the whole crawl.
    public Task<T> ExecuteWithDirectFallbackAsync<T>(
        Func<ProxyInfo?, CancellationToken, Task<RetryAttempt<T>>> attempt,
        Func<T> onExhausted,
        CancellationToken cancellationToken)
        => RunAsync(attempt, onExhausted, directFallbackOnEmpty: true, cancellationToken);

    // Synchronous sibling of ExecuteAsync, for callers that must issue a blocking send (the JS runtime
    // resolves module imports and its fetch shim synchronously to fit its single-threaded drain loop).
    public T Execute<T>(
        Func<ProxyInfo?, CancellationToken, RetryAttempt<T>> attempt,
        Func<T> onExhausted,
        CancellationToken cancellationToken)
    {
        var hasAlternativeRoute = _pool is not null && _pool.Proxies.Count > 1;

        for (var i = 0; i <= _options.MaxRetries; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProxyInfo? proxy = null;
            if (_pool is not null)
            {
                proxy = _pool.Acquire();
                if (proxy is null)
                    throw new ProxyPoolExhaustedException("No healthy proxies remain (below configured cutoff).");
            }

            RetryAttempt<T> result;
            try
            {
                result = AttemptOnce(attempt, proxy, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                result = RetryAttempt<T>.Failed(RetryReason.Timeout);
            }

            if (result.Succeeded)
            {
                if (proxy is not null)
                    _pool!.ReportSuccess(proxy);

                return result.Value;
            }

            if (proxy is not null)
                _pool!.ReportFailure(proxy, result.Failure!.Value);

            if (i < _options.MaxRetries && ShouldDelay(result.Failure!.Value, hasAlternativeRoute))
                Thread.Sleep(Backoff(i));
        }

        return onExhausted();
    }

    private async Task<T> RunAsync<T>(
        Func<ProxyInfo?, CancellationToken, Task<RetryAttempt<T>>> attempt,
        Func<T> onExhausted,
        bool directFallbackOnEmpty,
        CancellationToken cancellationToken)
    {
        // A multi-proxy pool can hand out a fresh route instantly, so rotation retries need no delay;
        // any other case is retrying the same effective target and must back off.
        var hasAlternativeRoute = _pool is not null && _pool.Proxies.Count > 1;

        for (var i = 0; i <= _options.MaxRetries; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ProxyInfo? proxy = null;
            if (_pool is not null)
            {
                proxy = _pool.Acquire();
                if (proxy is null)
                {
                    if (directFallbackOnEmpty)
                        return (await AttemptOnce(attempt, null, cancellationToken).ConfigureAwait(false)).Value;

                    throw new ProxyPoolExhaustedException("No healthy proxies remain (below configured cutoff).");
                }
            }

            RetryAttempt<T> result;
            try
            {
                result = await AttemptOnce(attempt, proxy, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                // The linked token fired, i.e. the per-attempt timeout elapsed (not the caller).
                result = RetryAttempt<T>.Failed(RetryReason.Timeout);
            }

            if (result.Succeeded)
            {
                if (proxy is not null)
                    _pool!.ReportSuccess(proxy);

                return result.Value;
            }

            if (proxy is not null)
                _pool!.ReportFailure(proxy, result.Failure!.Value);

            if (i < _options.MaxRetries && ShouldDelay(result.Failure!.Value, hasAlternativeRoute))
                await _delay(Backoff(i), cancellationToken).ConfigureAwait(false);
        }

        return onExhausted();
    }

    private async Task<RetryAttempt<T>> AttemptOnce<T>(
        Func<ProxyInfo?, CancellationToken, Task<RetryAttempt<T>>> attempt,
        ProxyInfo? proxy,
        CancellationToken cancellationToken)
    {
        if (_options.AttemptTimeout <= TimeSpan.Zero)
            return await attempt(proxy, cancellationToken).ConfigureAwait(false);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.AttemptTimeout);
        return await attempt(proxy, timeoutCts.Token).ConfigureAwait(false);
    }

    private RetryAttempt<T> AttemptOnce<T>(
        Func<ProxyInfo?, CancellationToken, RetryAttempt<T>> attempt,
        ProxyInfo? proxy,
        CancellationToken cancellationToken)
    {
        if (_options.AttemptTimeout <= TimeSpan.Zero)
            return attempt(proxy, cancellationToken);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_options.AttemptTimeout);
        return attempt(proxy, timeoutCts.Token);
    }

    private bool ShouldDelay(RetryReason reason, bool hasAlternativeRoute)
    {
        if (!hasAlternativeRoute)
            return true;

        return _options.DelayOnRateLimit && reason == RetryReason.RateLimited;
    }

    private TimeSpan Backoff(int attemptIndex)
    {
        var exponential = _options.BaseDelay.TotalMilliseconds * Math.Pow(2, attemptIndex);
        var capped = Math.Min(exponential, _options.MaxDelay.TotalMilliseconds);
        var jitter = 1 + ((Random.Shared.NextDouble() * 2) - 1) * _options.JitterFactor;

        return TimeSpan.FromMilliseconds(Math.Max(0, capped * jitter));
    }
}
