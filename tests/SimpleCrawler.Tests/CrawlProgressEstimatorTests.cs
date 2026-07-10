using SimpleCrawler.Core.Progress;
using System.Diagnostics;

namespace SimpleCrawler.Tests;

public class CrawlProgressEstimatorTests
{
    private const double _pagesPerSecond = 4.0;

    [Fact]
    public void WarmingUp_Until_Enough_Samples()
    {
        var estimator = new CrawlProgressEstimator(new ProgressOptions { MinPagesBeforeEstimate = 20 });

        estimator.AddSample(1, 5, Ticks(0.0));
        estimator.AddSample(2, 9, Ticks(0.25));

        Assert.Equal(ProgressState.WarmingUp, estimator.Compute(10000).State);
    }

    [Fact]
    public void Reports_Expanding_While_Yield_Exceeds_One()
    {
        var trajectory = SimulateCrawl(initialYield: 3.0, decay: 0.985);
        var estimator = FeedUpTo(trajectory, page: 50);

        var estimate = estimator.Compute(10000);

        Assert.Equal(ProgressState.Expanding, estimate.State);
        Assert.True(estimate.Yield > 1, $"expected yield > 1 while expanding, got {estimate.Yield}");
    }

    [Fact]
    public void Estimate_Converges_As_Frontier_Drains()
    {
        var trajectory = SimulateCrawl(initialYield: 3.0, decay: 0.985);
        var final = trajectory.Count;

        var early = FeedUpTo(trajectory, page: (int)(final * 0.6)).Compute(10000);
        var late = FeedUpTo(trajectory, page: (int)(final * 0.9)).Compute(10000);

        Assert.Equal(ProgressState.Estimating, early.State);
        Assert.Equal(ProgressState.Estimating, late.State);

        // Yield has fallen below one and the frontier is contracting.
        Assert.True(early.Yield < 1);

        // The tangent-line projection overshoots the true finish early and tightens toward it.
        Assert.InRange(early.EstimatedTotalPages, final * 0.9, final * 1.5);
        Assert.InRange(late.EstimatedTotalPages, final * 0.97, final * 1.15);

        var earlyError = Math.Abs(early.EstimatedTotalPages - final);
        var lateError = Math.Abs(late.EstimatedTotalPages - final);
        Assert.True(lateError <= earlyError, $"expected the estimate to converge: early err {earlyError:0.0}, late err {lateError:0.0}");
    }

    [Fact]
    public void Measures_Throughput_From_The_Window()
    {
        var trajectory = SimulateCrawl(initialYield: 3.0, decay: 0.985);
        var estimate = FeedUpTo(trajectory, page: (int)(trajectory.Count * 0.8)).Compute(10000);

        Assert.InRange(estimate.Throughput, _pagesPerSecond - 0.5, _pagesPerSecond + 0.5);
    }

    [Fact]
    public void Clamps_Projection_To_The_Page_Budget()
    {
        var trajectory = SimulateCrawl(initialYield: 3.0, decay: 0.985);
        var page = (int)(trajectory.Count * 0.6);
        var maxPages = page + 5;

        var estimate = FeedUpTo(trajectory, page).Compute(maxPages);

        Assert.True(estimate.EstimatedTotalPages <= maxPages + 1e-6);
    }

    [Fact]
    public void Withholds_Eta_Until_Drain_Is_Sustained()
    {
        var options = new ProgressOptions { WindowSize = 20, MinPagesBeforeEstimate = 5, SustainedDrain = TimeSpan.FromSeconds(20) };
        var estimator = new CrawlProgressEstimator(options);

        // Steady drain from the first sample: +10 processed/s, +5 discovered/s (yield 0.5), pending falls.
        var early = default(ProgressEstimate);
        var late = default(ProgressEstimate);
        for (var t = 1; t <= 30; t++)
        {
            estimator.AddSample(10 * t, 200 + 5 * t, Ticks(t));
            if (t == 10)
                early = estimator.Compute(100000);
            if (t == 28)
                late = estimator.Compute(100000);
        }

        // Frontier is shrinking the whole time, but the ETA is held back until the drain has persisted.
        Assert.Equal(ProgressState.Draining, early.State);
        Assert.Null(early.EtaLow);

        Assert.Equal(ProgressState.Estimating, late.State);
        Assert.NotNull(late.EtaLow);
    }

    [Fact]
    public void Reverts_To_Expanding_When_A_Discovery_Cliff_Hits()
    {
        var options = new ProgressOptions { WindowSize = 20, MinPagesBeforeEstimate = 5, SustainedDrain = TimeSpan.FromSeconds(20) };
        var estimator = new CrawlProgressEstimator(options);

        for (var t = 1; t <= 25; t++)
            estimator.AddSample(10 * t, 200 + 5 * t, Ticks(t));

        Assert.Equal(ProgressState.Estimating, estimator.Compute(100000).State);

        // A cliff: a run of pages each revealing a burst of new links (yield far above 1).
        var processed = 250;
        var discovered = 325;
        for (var k = 1; k <= 15; k++)
        {
            processed += 2;
            discovered += 80;
            estimator.AddSample(processed, discovered, Ticks(25 + k));
        }

        Assert.Equal(ProgressState.Expanding, estimator.Compute(100000).State);
    }

    // Walks a crawl one processed page at a time. Each page yields a decaying number of new URLs; the
    // frontier grows while yield > 1 and drains once it falls below. Returns the per-page (processed,
    // discovered) snapshots up to the point the frontier empties.
    private static List<(int Processed, int Discovered)> SimulateCrawl(double initialYield, double decay)
    {
        var states = new List<(int, int)>();

        var discovered = 1.0;
        for (var processed = 1; processed <= 100000; processed++)
        {
            discovered += initialYield * Math.Pow(decay, processed - 1);
            var discoveredCount = (int)Math.Round(discovered);
            states.Add((processed, discoveredCount));

            if (discoveredCount - processed <= 0)
                break;
        }

        return states;
    }

    private static CrawlProgressEstimator FeedUpTo(List<(int Processed, int Discovered)> trajectory, int page)
    {
        // SustainedDrain disabled here so these tests exercise the projection math directly; the drain
        // gate has its own tests below.
        var estimator = new CrawlProgressEstimator(new ProgressOptions { WindowSize = 20, MinPagesBeforeEstimate = 20, SustainedDrain = TimeSpan.Zero });

        foreach (var (processed, discovered) in trajectory)
        {
            estimator.AddSample(processed, discovered, Ticks(processed / _pagesPerSecond));
            if (processed >= page)
                break;
        }

        return estimator;
    }

    private static long Ticks(double seconds) => (long)(seconds * Stopwatch.Frequency);
}
