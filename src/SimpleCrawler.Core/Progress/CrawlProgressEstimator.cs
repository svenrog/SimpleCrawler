using System.Diagnostics;

namespace SimpleCrawler.Core.Progress;

/// <summary>
/// Projects how long a crawl still has to run from a bounded, recent slice of its history.
/// </summary>
/// <remarks>
/// The crawl ends when processed catches discovered, i.e. when the pending frontier F = D - P drains.
/// Over the window we fit two lines: throughput mu = dP/dt (pages/sec) and yield g = dD/dP (new URLs per
/// processed page, the quantity that decays as the site saturates). Once g &lt; 1 the frontier is
/// contracting and the remaining work is the geometric sum F + gF + g^2F + ... = F / (1 - g); dividing by
/// mu turns that into an ETA. The g estimate carries its regression standard error, which fans out into
/// an optimistic/pessimistic ETA band.
/// <para>
/// A discovery cliff (a page that reveals a whole new section) can't be predicted from local history, so
/// a lone window seeing g &lt; 1 is not trusted: an ETA is only emitted once the frontier has been
/// contracting for a sustained period. A burst that pushes g back to &gt;= 1 resets that clock, so the
/// estimate reverts to "expanding" rather than whipsawing between confident-but-wrong numbers.
/// </para>
/// </remarks>
public sealed class CrawlProgressEstimator
{
    /// <summary>
    /// Kept just below 1 so the geometric remaining-work sum stays finite; a page budget clamps it further.
    /// </summary>
    private const double _maxYield = 0.999;

    private readonly ProgressOptions _options;

    private readonly double[] _seconds;
    private readonly int[] _processed;
    private readonly int[] _discovered;
    private readonly int _capacity;

    private int _head;
    private int _count;

    private double _latestSeconds;
    private double? _drainStartSeconds;

    public CrawlProgressEstimator(ProgressOptions options)
    {
        _options = options;
        _capacity = Math.Max(3, options.WindowSize);
        _seconds = new double[_capacity];
        _processed = new int[_capacity];
        _discovered = new int[_capacity];
    }

    /// <summary>
    /// Records one observation of the crawl's counters. Call periodically on a fixed cadence.
    /// </summary>
    /// <param name="processed">Pages fully processed so far.</param>
    /// <param name="discovered">Distinct URLs discovered so far.</param>
    /// <param name="timestamp">A <see cref="Stopwatch.GetTimestamp"/> reading for this sample.</param>
    public void AddSample(int processed, int discovered, long timestamp)
    {
        var seconds = (double)timestamp / Stopwatch.Frequency;

        _seconds[_head] = seconds;
        _processed[_head] = processed;
        _discovered[_head] = discovered;

        _head = (_head + 1) % _capacity;
        if (_count < _capacity)
            _count++;

        _latestSeconds = seconds;
        UpdateDrainStreak(processed, seconds);
    }

    /// <summary>
    /// Tracks how long the frontier has been contracting without interruption. A window that is still
    /// expanding (or a burst that flips it back to expanding) clears the streak, so the sustained-drain
    /// gate has to be earned again from scratch.
    /// </summary>
    private void UpdateDrainStreak(int processed, double seconds)
    {
        if (_count < 3 || processed < _options.MinPagesBeforeEstimate || !TryComputeSlopes(out _, out var yield, out _))
        {
            _drainStartSeconds = null;
            return;
        }

        if (yield < 1)
            _drainStartSeconds ??= seconds;
        else
            _drainStartSeconds = null;
    }

    /// <summary>
    /// Produces the current estimate from the window of samples collected so far.
    /// </summary>
    /// <param name="maxPages">The crawl's page budget, used to clamp the projected total.</param>
    public ProgressEstimate Compute(int maxPages)
    {
        if (_count == 0)
            return new ProgressEstimate { State = ProgressState.WarmingUp };

        var newest = (_head - 1 + _capacity) % _capacity;
        var processed = _processed[newest];
        var discovered = _discovered[newest];
        var pending = discovered - processed;

        var estimate = new ProgressEstimate
        {
            State = ProgressState.WarmingUp,
            Processed = processed,
            Discovered = discovered,
            Pending = pending,
        };

        if (_count < 3 || processed < _options.MinPagesBeforeEstimate)
            return estimate;

        if (!TryComputeSlopes(out var throughput, out var yield, out var yieldStdErr) || throughput <= 0)
            return estimate;

        estimate = estimate with { Throughput = throughput, Yield = yield };

        if (yield >= 1)
            return estimate with { State = ProgressState.Expanding };

        var drainedFor = _drainStartSeconds is { } start ? _latestSeconds - start : 0;
        if (drainedFor < _options.SustainedDrain.TotalSeconds)
            return estimate with { State = ProgressState.Draining };

        var budget = maxPages > 0 ? maxPages - processed : double.PositiveInfinity;

        var remaining = Remaining(pending, yield, budget);
        var remainingLow = Remaining(pending, Math.Clamp(yield - yieldStdErr, 0, _maxYield), budget);
        var remainingHigh = Remaining(pending, Math.Clamp(yield + yieldStdErr, 0, _maxYield), budget);

        return estimate with
        {
            State = ProgressState.Estimating,
            RemainingPages = remaining,
            EstimatedTotalPages = processed + remaining,
            EtaLow = TimeSpan.FromSeconds(remainingLow / throughput),
            EtaHigh = TimeSpan.FromSeconds(remainingHigh / throughput),
        };
    }

    private static double Remaining(int pending, double yield, double budget)
    {
        var remaining = pending / (1 - yield);
        return Math.Min(remaining, budget);
    }

    private bool TryComputeSlopes(out double throughput, out double yield, out double yieldStdErr)
    {
        throughput = 0;
        yield = 0;
        yieldStdErr = 0;

        if (_count < 3)
            return false;

        var seconds = new double[_count];
        var proc = new double[_count];
        var disc = new double[_count];
        var oldest = (_head - _count + _capacity) % _capacity;
        for (var i = 0; i < _count; i++)
        {
            var idx = (oldest + i) % _capacity;
            seconds[i] = _seconds[idx];
            proc[i] = _processed[idx];
            disc[i] = _discovered[idx];
        }

        if (!TryFit(seconds, proc, out throughput, out _) || throughput <= 0)
            return false;

        return TryFit(proc, disc, out yield, out yieldStdErr);
    }

    /// <summary>
    /// Ordinary least-squares slope of y on x, plus the standard error of that slope (0 when there are
    /// too few points to estimate residual spread).
    /// </summary>
    /// <returns><c>false</c> when x has no spread to regress against; otherwise <c>true</c>.</returns>
    private static bool TryFit(double[] xs, double[] ys, out double slope, out double slopeStdErr)
    {
        slope = 0;
        slopeStdErr = 0;

        var n = xs.Length;
        double sumX = 0, sumY = 0;
        for (var i = 0; i < n; i++)
        {
            sumX += xs[i];
            sumY += ys[i];
        }

        var meanX = sumX / n;
        var meanY = sumY / n;

        double sxx = 0, sxy = 0;
        for (var i = 0; i < n; i++)
        {
            var dx = xs[i] - meanX;
            sxx += dx * dx;
            sxy += dx * (ys[i] - meanY);
        }

        if (sxx <= double.Epsilon)
            return false;

        slope = sxy / sxx;

        if (n > 2)
        {
            var intercept = meanY - slope * meanX;
            double sse = 0;
            for (var i = 0; i < n; i++)
            {
                var residual = ys[i] - (intercept + slope * xs[i]);
                sse += residual * residual;
            }

            slopeStdErr = Math.Sqrt(sse / (n - 2) / sxx);
        }

        return true;
    }
}
