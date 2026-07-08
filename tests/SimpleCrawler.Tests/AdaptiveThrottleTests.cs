using Microsoft.Extensions.Logging.Abstractions;
using SimpleCrawler.Core.Throttling;
using System.Diagnostics;

namespace SimpleCrawler.Tests;

// Guards the adaptive per-host throttle: a rate-limit response raises the host's effective crawl delay,
// a Retry-After grace stalls the next fetch, sustained success decays the penalty, and the whole thing is
// inert when disabled.
public class AdaptiveThrottleTests
{
    private const string _host = "localhost";

    [Fact]
    public void RateLimit_Raises_And_Grows_Effective_Delay()
    {
        var throttler = Create(new ThrottleOptions());

        Assert.Equal(0, throttler.GetEffectiveDelay(_host, 0), 3);

        throttler.ReportRateLimited(_host, retryAfter: null);
        Assert.Equal(1, throttler.GetEffectiveDelay(_host, 0), 3);

        throttler.ReportRateLimited(_host, retryAfter: null);
        Assert.Equal(2, throttler.GetEffectiveDelay(_host, 0), 3);

        throttler.ReportRateLimited(_host, retryAfter: null);
        Assert.Equal(4, throttler.GetEffectiveDelay(_host, 0), 3);
    }

    [Fact]
    public void Penalty_Is_Capped_At_Max()
    {
        var throttler = Create(new ThrottleOptions { MaxDelaySeconds = 3 });

        for (var i = 0; i < 6; i++)
            throttler.ReportRateLimited(_host, retryAfter: null);

        Assert.Equal(3, throttler.GetEffectiveDelay(_host, 0), 3);
    }

    [Fact]
    public void Sustained_Success_Decays_Penalty()
    {
        var throttler = Create(new ThrottleOptions());

        throttler.ReportRateLimited(_host, retryAfter: null);
        throttler.ReportRateLimited(_host, retryAfter: null);
        Assert.Equal(2, throttler.GetEffectiveDelay(_host, 0), 3);

        // Decay only kicks in after a run of successes; the ninth leaves the penalty untouched.
        for (var i = 0; i < 9; i++)
            throttler.ReportSuccess(_host);
        Assert.Equal(2, throttler.GetEffectiveDelay(_host, 0), 3);

        throttler.ReportSuccess(_host);
        Assert.Equal(1, throttler.GetEffectiveDelay(_host, 0), 3);
    }

    [Fact]
    public void Disabled_Ignores_Rate_Limit()
    {
        var throttler = Create(new ThrottleOptions { Enabled = false });

        throttler.ReportRateLimited(_host, retryAfter: null);

        Assert.Equal(0, throttler.GetEffectiveDelay(_host, 0), 3);
    }

    [Fact]
    public async Task RetryAfter_Grace_Stalls_Next_Fetch()
    {
        var throttler = Create(new ThrottleOptions());

        throttler.ReportRateLimited(_host, retryAfter: TimeSpan.FromMilliseconds(400));

        var stopwatch = Stopwatch.StartNew();
        await throttler.WaitAsync(_host, baseDelay: 0, TestContext.Current.CancellationToken);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed >= TimeSpan.FromMilliseconds(300),
            $"Expected the Retry-After grace to stall the next fetch; elapsed {stopwatch.ElapsedMilliseconds}ms.");
    }

    private static AdaptiveThrottler Create(ThrottleOptions options)
    {
        var throttler = new AdaptiveThrottler(options, NullLogger.Instance);
        throttler.Reset([_host]);
        return throttler;
    }
}
