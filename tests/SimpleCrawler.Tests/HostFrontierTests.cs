using Microsoft.Extensions.Logging.Abstractions;
using SimpleCrawler.Core.Scheduling;
using SimpleCrawler.Core.Throttling;

namespace SimpleCrawler.Tests;

/// <summary>
/// Guards the host-partitioned frontier: a slow host never holds up URLs bound for a ready host, and
/// equally-ready hosts are dispatched round-robin rather than one host drained at a time.
/// </summary>
public class HostFrontierTests
{
    [Fact]
    public async Task Slow_Host_Does_Not_Block_A_Ready_Host()
    {
        var throttler = new AdaptiveThrottler(new ThrottleOptions(), NullLogger.Instance);
        throttler.Reset(["a", "b"]);

        // Host "a" is heavily spaced; host "b" is unthrottled.
        var frontier = new HostFrontier(throttler, authority => authority == "a" ? 100d : 0d);

        // A burst of slow-host URLs is enqueued ahead of the ready host, as happens when one page yields
        // many same-host links.
        frontier.Enqueue("http://a/1");
        frontier.Enqueue("http://a/2");
        frontier.Enqueue("http://b/1");
        frontier.Enqueue("http://b/2");

        var ct = TestContext.Current.CancellationToken;

        // The slow host's first slot is immediate, but its second is 100s out - both ready-host URLs must be
        // dispatched before that, instead of the queue draining in FIFO order.
        Assert.Equal("http://a/1", await frontier.DequeueReadyAsync(ct));
        Assert.Equal("http://b/1", await frontier.DequeueReadyAsync(ct));
        Assert.Equal("http://b/2", await frontier.DequeueReadyAsync(ct));
    }

    [Fact]
    public async Task Equally_Ready_Hosts_Are_Dispatched_Round_Robin()
    {
        var throttler = new AdaptiveThrottler(new ThrottleOptions(), NullLogger.Instance);
        throttler.Reset(["a", "b", "c"]);

        var frontier = new HostFrontier(throttler, _ => 0d);

        // Seed each host's URLs contiguously - the worst case for a FIFO frontier.
        frontier.Enqueue("http://a/1");
        frontier.Enqueue("http://a/2");
        frontier.Enqueue("http://b/1");
        frontier.Enqueue("http://b/2");
        frontier.Enqueue("http://c/1");
        frontier.Enqueue("http://c/2");

        var ct = TestContext.Current.CancellationToken;

        var order = new List<string?>();
        for (var i = 0; i < 6; i++)
            order.Add(await frontier.DequeueReadyAsync(ct));

        Assert.Equal(
            ["http://a/1", "http://b/1", "http://c/1", "http://a/2", "http://b/2", "http://c/2"],
            order);
    }

    [Fact]
    public async Task Completed_Frontier_Drains_Remaining_Then_Returns_Null()
    {
        var throttler = new AdaptiveThrottler(new ThrottleOptions(), NullLogger.Instance);
        throttler.Reset(["a"]);

        var frontier = new HostFrontier(throttler, _ => 0d);
        frontier.Enqueue("http://a/1");
        frontier.Enqueue("http://a/2");
        frontier.Complete();

        Assert.False(frontier.Enqueue("http://a/3"));

        var ct = TestContext.Current.CancellationToken;
        Assert.Equal("http://a/1", await frontier.DequeueReadyAsync(ct));
        Assert.Equal("http://a/2", await frontier.DequeueReadyAsync(ct));
        Assert.Null(await frontier.DequeueReadyAsync(ct));
    }
}
