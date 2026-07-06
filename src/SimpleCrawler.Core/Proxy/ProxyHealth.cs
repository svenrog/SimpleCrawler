using System.Diagnostics;

namespace Crawler.Core.Proxy;

internal sealed class ProxyHealth
{
    private readonly ProxyPoolOptions _options;
    private readonly Lock _gate = new();
    private int _consecutiveFailures;
    private int _totalFailures;
    private long _benchedUntilTicks;

    public ProxyHealth(ProxyInfo proxy, ProxyPoolOptions options)
    {
        Proxy = proxy;
        _options = options;
    }

    public ProxyInfo Proxy { get; }

    public bool IsOverThreshold
    {
        get
        {
            lock (_gate)
            {
                return _consecutiveFailures >= _options.FailureThreshold;
            }
        }
    }

    public long BenchedUntilTicks
    {
        get
        {
            lock (_gate)
            {
                return _benchedUntilTicks;
            }
        }
    }

    public bool IsAvailable(long nowTicks)
    {
        lock (_gate)
        {
            return _consecutiveFailures < _options.FailureThreshold || nowTicks >= _benchedUntilTicks;
        }
    }

    public void RecordFailure()
    {
        lock (_gate)
        {
            _consecutiveFailures++;
            _totalFailures++;

            if (_consecutiveFailures >= _options.FailureThreshold)
            {
                var cooldownTicks = (long)(_options.Cooldown.TotalSeconds * Stopwatch.Frequency);
                _benchedUntilTicks = Stopwatch.GetTimestamp() + cooldownTicks;
            }
        }
    }

    public void RecordSuccess()
    {
        lock (_gate)
        {
            _consecutiveFailures = 0;
        }
    }
}
