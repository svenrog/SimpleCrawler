namespace SimpleCrawler.Core.Proxy;

// Single home for the acquire/report/retry loop shared by every proxied fetch path (static handler,
// headless crawlers, robots probes). Callers supply one classified attempt; this decides rotation,
// health reporting and exhaustion so that logic lives in exactly one place.
public sealed class ProxyRetryExecutor
{
    private readonly IProxyPool _pool;
    private readonly int _maxRetries;

    public ProxyRetryExecutor(IProxyPool pool, int maxRetries)
    {
        _pool = pool;
        _maxRetries = maxRetries;
    }

    // Rotates through proxies until one attempt succeeds. Exhausting the pool (below cutoff) aborts
    // the crawl; exhausting the retry budget yields onExhausted().
    public Task<T> ExecuteAsync<T>(
        Func<ProxyInfo?, CancellationToken, Task<ProxyAttempt<T>>> attempt,
        Func<T> onExhausted,
        CancellationToken cancellationToken)
        => RunAsync(attempt, onExhausted, directFallbackOnEmpty: false, cancellationToken);

    // As ExecuteAsync, but an empty pool falls back to a single un-proxied attempt instead of
    // aborting - used for robots/sitemap probes, which must not fail the whole crawl.
    public Task<T> ExecuteWithDirectFallbackAsync<T>(
        Func<ProxyInfo?, CancellationToken, Task<ProxyAttempt<T>>> attempt,
        Func<T> onExhausted,
        CancellationToken cancellationToken)
        => RunAsync(attempt, onExhausted, directFallbackOnEmpty: true, cancellationToken);

    private async Task<T> RunAsync<T>(
        Func<ProxyInfo?, CancellationToken, Task<ProxyAttempt<T>>> attempt,
        Func<T> onExhausted,
        bool directFallbackOnEmpty,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i <= _maxRetries; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var proxy = _pool.Acquire();
            if (proxy is null)
            {
                if (directFallbackOnEmpty)
                    return (await attempt(null, cancellationToken).ConfigureAwait(false)).Value;

                throw new ProxyPoolExhaustedException("No healthy proxies remain (below configured cutoff).");
            }

            var result = await attempt(proxy, cancellationToken).ConfigureAwait(false);

            if (result.Succeeded)
            {
                _pool.ReportSuccess(proxy);
                return result.Value;
            }

            _pool.ReportFailure(proxy, result.Failure!.Value);
        }

        return onExhausted();
    }
}
