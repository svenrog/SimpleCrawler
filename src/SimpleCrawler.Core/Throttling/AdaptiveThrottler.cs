using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SimpleCrawler.Core.Throttling;

/// <summary>
/// Per-host request spacing with an adaptive rate-limit penalty. Each authority gets its own HostThrottle
/// so one host's delay never stalls another; the base (configured/robots) delay is supplied per call so a
/// robots.txt Crawl-delay resolved after construction is still honoured.
/// </summary>
public sealed class AdaptiveThrottler
{
    private readonly ThrottleOptions _options;
    private readonly ILogger _logger;

    private Dictionary<string, HostThrottle> _hosts;

    public AdaptiveThrottler(ThrottleOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
        _hosts = new Dictionary<string, HostThrottle>(StringComparer.OrdinalIgnoreCase);
    }

    public void Reset(IEnumerable<string> authorities)
    {
        _hosts = new Dictionary<string, HostThrottle>(StringComparer.OrdinalIgnoreCase);
        foreach (var authority in authorities)
            _hosts[authority] = new HostThrottle();
    }

    /// <summary>
    /// Returns the Stopwatch timestamp at which this host's next fetch may fire, without reserving the slot,
    /// so a scheduler can order hosts by readiness before committing a URL to a worker. The caller passes a
    /// single shared <paramref name="now"/> so that every host ready at this instant reports the same value
    /// and ties break on the scheduler's own fairness order rather than on peek order. Unknown or unthrottled
    /// hosts read as ready now.
    /// </summary>
    public long PeekNextReady(string authority, double baseDelay, long now)
    {
        if (!_hosts.TryGetValue(authority, out var host))
            return now;

        var delay = Cap(baseDelay + host.Penalty);
        if (delay <= 0 && !host.IsActive)
            return now;

        return host.PeekNextSlot(now);
    }

    /// <summary>
    /// Reserves this host's next fetch slot and returns the Stopwatch timestamp the caller must wait until
    /// before fetching. A host with no spacing and no active penalty reserves nothing and fires immediately,
    /// so unthrottled hosts keep full throughput.
    /// </summary>
    public long ReserveSlot(string authority, double baseDelay)
    {
        var now = Stopwatch.GetTimestamp();
        if (!_hosts.TryGetValue(authority, out var host))
            return now;

        var delay = Cap(baseDelay + host.Penalty);
        if (delay <= 0 && !host.IsActive)
            return now;

        return host.Reserve(delay);
    }

    public async Task WaitAsync(string authority, double baseDelay, CancellationToken cancellationToken)
    {
        var slot = ReserveSlot(authority, baseDelay);
        var waitTicks = slot - Stopwatch.GetTimestamp();
        if (waitTicks > 0)
            await Task.Delay(TimeSpan.FromSeconds((double)waitTicks / Stopwatch.Frequency), cancellationToken);
    }

    public double GetEffectiveDelay(string authority, double baseDelay)
        => _hosts.TryGetValue(authority, out var host) ? Cap(baseDelay + host.Penalty) : Cap(baseDelay);

    public void ReportRateLimited(string authority, TimeSpan? retryAfter)
    {
        if (!_options.Enabled || !_hosts.TryGetValue(authority, out var host))
            return;

        var penalty = host.PenalizeRateLimit(_options.MaxDelaySeconds, retryAfter);

        _logger.LogWarning("Rate limited by '{authority}'; raising per-host delay to {penalty:0.##}s{grace}.",
            authority, penalty, retryAfter is { } grace ? $" (Retry-After {grace.TotalSeconds:0.##}s)" : string.Empty);
    }

    public void ReportSuccess(string authority)
    {
        if (!_options.Enabled || !_hosts.TryGetValue(authority, out var host))
            return;

        host.RegisterSuccess();
    }

    private double Cap(double delay)
    {
        var max = _options.MaxDelaySeconds;
        return max > 0 && delay > max ? max : delay;
    }
}
