using System.Diagnostics;
using SimpleCrawler.Tests.Common.Extensions;

namespace SimpleCrawler.Tests;

/// <summary>
/// Guards the bounded-teardown contract the host fixtures rely on: a wedged disposal must never pin the
/// test host, yet a healthy one must neither be delayed nor have its faults surfaced.
/// </summary>
public class TeardownGuardTests
{
    [Fact]
    public async Task RunBounded_Returns_When_Teardown_Never_Completes()
    {
        var stopwatch = Stopwatch.StartNew();

        // A teardown that never completes stands in for a wedged browser/host dispose. Only the deadline
        // may end the wait - deliberately not the test's own token, so the bound itself is what is proven.
        await TeardownGuard.RunBounded(
            () => new TaskCompletionSource().Task,
            TimeSpan.FromMilliseconds(200));

        stopwatch.Stop();
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"Teardown was not bounded by the deadline (took {stopwatch.Elapsed}).");
    }

    [Fact]
    public async Task RunBounded_Completes_Promptly_When_Teardown_Is_Fast()
    {
        var stopwatch = Stopwatch.StartNew();
        var ran = false;

        await TeardownGuard.RunBounded(
            () => { ran = true; return Task.CompletedTask; },
            TimeSpan.FromSeconds(30));

        stopwatch.Stop();
        Assert.True(ran);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5),
            $"A fast teardown was needlessly delayed (took {stopwatch.Elapsed}).");
    }

    [Fact]
    public async Task RunBounded_Swallows_Teardown_Faults()
    {
        await TeardownGuard.RunBounded(
            () => Task.FromException(new InvalidOperationException("teardown blew up")),
            TimeSpan.FromSeconds(30));
    }
}
