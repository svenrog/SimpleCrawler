using System.Diagnostics;

namespace SimpleCrawler.Core.Proxy;

public sealed class ProxyPool : IProxyPool
{
    private readonly ProxyHealth[] _health;
    private readonly Dictionary<ProxyInfo, ProxyHealth> _byProxy;
    private readonly ProxyPoolOptions _options;
    private int _cursor;

    public ProxyPool(IReadOnlyList<ProxyInfo> proxies, ProxyPoolOptions options)
    {
        if (proxies is null || proxies.Count == 0)
            throw new ArgumentException("Proxy list cannot be empty.", nameof(proxies));

        _options = options;
        _health = proxies.Select(p => new ProxyHealth(p, options)).ToArray();
        _byProxy = _health.ToDictionary(h => h.Proxy);
    }

    public ProxyInfo? Acquire()
    {
        var length = _health.Length;

        // Abort when too few proxies are structurally viable. Skipped for a single-proxy pool:
        // one configured proxy should fail-and-skip (or wait out its cooldown), not abort the crawl.
        if (length > 1 && Snapshot().HealthyRatio < _options.MinHealthyRatio)
            return null;

        var now = Stopwatch.GetTimestamp();
        var start = (int)((uint)Interlocked.Increment(ref _cursor) % (uint)length);

        ProxyHealth? earliest = null;
        for (var i = 0; i < length; i++)
        {
            var candidate = _health[(start + i) % length];
            if (candidate.IsAvailable(now))
                return candidate.Proxy;

            if (earliest is null || candidate.BenchedUntilTicks < earliest.BenchedUntilTicks)
                earliest = candidate;
        }

        // Everything is momentarily cooling down (ratio still above cutoff, else we returned null
        // above). Hand back the soonest-to-recover rather than tearing down a transient state.
        return earliest?.Proxy;
    }

    public void ReportSuccess(ProxyInfo proxy)
    {
        if (_byProxy.TryGetValue(proxy, out var health))
            health.RecordSuccess();
    }

    public void ReportFailure(ProxyInfo proxy, ProxyFailureKind kind)
    {
        if (_byProxy.TryGetValue(proxy, out var health))
            health.RecordFailure();
    }

    public ProxyPoolSnapshot Snapshot()
    {
        var healthy = 0;
        foreach (var health in _health)
        {
            if (!health.IsOverThreshold)
                healthy++;
        }

        return new ProxyPoolSnapshot { Total = _health.Length, Healthy = healthy };
    }
}
