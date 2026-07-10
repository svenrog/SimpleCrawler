using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace SimpleCrawler.Core.Progress;

/// <summary>
/// Background loop that periodically samples a crawl's progress into a <see cref="CrawlProgressEstimator"/>
/// and logs a human-readable estimate. Runs alongside the crawl workers and stops when its token is
/// cancelled; owns all progress logging so the crawler itself stays free of it.
/// </summary>
internal sealed class CrawlProgressReporter
{
    private readonly ProgressOptions _options;
    private readonly ILogger _logger;

    public CrawlProgressReporter(ProgressOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Samples <paramref name="sample"/> every <see cref="ProgressOptions.SampleInterval"/> and emits a
    /// progress line every <see cref="ProgressOptions.LogInterval"/> until cancelled.
    /// </summary>
    /// <param name="sample">Reads the current (processed, discovered) counts; called on the reporter thread.</param>
    /// <param name="maxPages">The crawl's page budget, used to clamp the projected total.</param>
    public async Task RunAsync(Func<(int Processed, int Discovered)> sample, int maxPages, CancellationToken cancellationToken)
    {
        var estimator = new CrawlProgressEstimator(_options);
        var lastLog = Stopwatch.GetTimestamp();

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_options.SampleInterval, cancellationToken);

                var (processed, discovered) = sample();
                estimator.AddSample(processed, discovered, Stopwatch.GetTimestamp());

                if (Stopwatch.GetElapsedTime(lastLog) < _options.LogInterval)
                    continue;

                lastLog = Stopwatch.GetTimestamp();
                Log(estimator.Compute(maxPages));
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void Log(ProgressEstimate estimate)
    {
        switch (estimate.State)
        {
            case ProgressState.Estimating:
                _logger.LogInformation("Crawl progress: '{processed}' of ~'{total}' pages, est. '{eta}' remaining at '{rate}/s'.",
                    estimate.Processed, Math.Round(estimate.EstimatedTotalPages).ToString("N0"),
                    FormatEta(estimate.EtaLow!.Value, estimate.EtaHigh!.Value), estimate.Throughput.ToString("0.0"));
                break;

            case ProgressState.Draining:
                _logger.LogInformation("Crawl progress: '{processed}' processed, '{pending}' queued — queue shrinking, confirming estimate.",
                    estimate.Processed, estimate.Pending);
                break;

            case ProgressState.Expanding:
                _logger.LogInformation("Crawl progress: '{processed}' processed, '{pending}' queued — still expanding.",
                    estimate.Processed, estimate.Pending);
                break;

            default:
                _logger.LogInformation("Crawl progress: '{processed}' processed, '{pending}' queued — estimating…",
                    estimate.Processed, estimate.Pending);
                break;
        }
    }

    private static string FormatEta(TimeSpan low, TimeSpan high)
    {
        var lowText = HumanizeDuration(low);
        var highText = HumanizeDuration(high);
        return lowText == highText ? $"~{lowText}" : $"~{lowText}–{highText}";
    }

    private static string HumanizeDuration(TimeSpan duration)
    {
        if (duration.TotalSeconds < 90)
            return $"{Math.Max(1, Math.Round(duration.TotalSeconds)):0} s";

        if (duration.TotalMinutes < 90)
            return $"{Math.Round(duration.TotalMinutes):0} min";

        return $"{(int)duration.TotalHours} h {duration.Minutes} min";
    }
}
