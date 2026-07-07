using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Retry;

namespace SimpleCrawler.Tests;

public class ProxyPoolTests
{
    private static ProxyInfo Info(string host) => new()
    {
        Host = host,
        Port = 1080,
        Protocol = ProxyProtocol.Http,
    };

    [Fact]
    public void Acquire_RoundRobins_Through_All_Proxies()
    {
        var pool = new ProxyPool([Info("a"), Info("b"), Info("c")], new ProxyPoolOptions());

        var seen = new HashSet<ProxyInfo>
        {
            pool.Acquire()!,
            pool.Acquire()!,
            pool.Acquire()!,
        };

        Assert.Equal(3, seen.Count);
    }

    [Fact]
    public void Benched_Proxy_Is_Skipped_Until_Cooldown_Expires()
    {
        var first = Info("a");
        var second = Info("b");
        var pool = new ProxyPool([first, second], new ProxyPoolOptions
        {
            FailureThreshold = 3,
            Cooldown = TimeSpan.FromMilliseconds(60),
            MinHealthyRatio = 0.0,
        });

        var a = pool.Acquire()!;
        var b = pool.Acquire()!;
        Assert.NotEqual(a, b);
        var dead = a;

        for (var i = 0; i < 3; i++)
            pool.ReportFailure(dead, RetryReason.Connection);

        for (var i = 0; i < 5; i++)
            Assert.Equal(b, pool.Acquire());
    }

    [Fact]
    public async Task Benched_Proxy_Is_Readmitted_After_Cooldown()
    {
        var first = Info("a");
        var second = Info("b");
        var pool = new ProxyPool([first, second], new ProxyPoolOptions
        {
            FailureThreshold = 3,
            Cooldown = TimeSpan.FromMilliseconds(60),
            MinHealthyRatio = 0.0,
        });

        var dead = pool.Acquire()!;
        for (var i = 0; i < 3; i++)
            pool.ReportFailure(dead, RetryReason.Connection);

        await Task.Delay(130, TestContext.Current.CancellationToken);

        var seen = new HashSet<ProxyInfo>();
        for (var i = 0; i < 10; i++)
            seen.Add(pool.Acquire()!);

        Assert.Contains(dead, seen);
    }

    [Fact]
    public void Aborts_When_Healthy_Ratio_Drops_Below_Cutoff()
    {
        var pool = new ProxyPool([Info("a"), Info("b")], new ProxyPoolOptions
        {
            FailureThreshold = 1,
            Cooldown = TimeSpan.FromSeconds(30),
            MinHealthyRatio = 0.6,
        });

        pool.ReportFailure(Info("a"), RetryReason.Connection);

        Assert.Null(pool.Acquire());
    }

    [Fact]
    public void Continues_When_Enough_Proxies_Remain()
    {
        var proxies = new[] { Info("a"), Info("b"), Info("c"), Info("d") };
        var pool = new ProxyPool(proxies, new ProxyPoolOptions
        {
            FailureThreshold = 1,
            Cooldown = TimeSpan.FromSeconds(30),
            MinHealthyRatio = 0.3,
        });

        pool.ReportFailure(proxies[0], RetryReason.Connection);
        pool.ReportFailure(proxies[1], RetryReason.Connection);

        Assert.NotNull(pool.Acquire());
    }

    [Fact]
    public void Single_Proxy_Pool_Never_Aborts()
    {
        var only = Info("a");
        var pool = new ProxyPool([only], new ProxyPoolOptions
        {
            FailureThreshold = 1,
            Cooldown = TimeSpan.FromSeconds(30),
            MinHealthyRatio = 0.99,
        });

        pool.ReportFailure(only, RetryReason.Connection);

        Assert.NotNull(pool.Acquire());
    }

    [Fact]
    public void Snapshot_Reports_Healthy_Count()
    {
        var proxies = new[] { Info("a"), Info("b") };
        var pool = new ProxyPool(proxies, new ProxyPoolOptions { FailureThreshold = 2 });

        Assert.Equal(2, pool.Snapshot().Healthy);

        pool.ReportFailure(proxies[0], RetryReason.Connection);
        pool.ReportFailure(proxies[0], RetryReason.Connection);

        var snapshot = pool.Snapshot();
        Assert.Equal(1, snapshot.Healthy);
        Assert.Equal(0.5, snapshot.HealthyRatio);
    }

    [Fact]
    public void Success_Resets_Consecutive_Failures()
    {
        var proxies = new[] { Info("a"), Info("b") };
        var pool = new ProxyPool(proxies, new ProxyPoolOptions { FailureThreshold = 2 });

        pool.ReportFailure(proxies[0], RetryReason.Connection);
        pool.ReportSuccess(proxies[0]);
        pool.ReportFailure(proxies[0], RetryReason.Connection);

        Assert.Equal(2, pool.Snapshot().Healthy);
    }
}
