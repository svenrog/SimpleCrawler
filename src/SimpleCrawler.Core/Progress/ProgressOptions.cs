namespace SimpleCrawler.Core.Progress;

/// <summary>
/// Tuning for the live crawl-time estimate: how often it samples, how much recent history it keeps, how
/// often it logs, and how much certainty it demands before showing an ETA.
/// </summary>
public sealed class ProgressOptions
{
    /// <summary>Whether the crawl logs periodic time estimates at all.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// How often the reporter snapshots (processed, discovered) into its bounded history slice.
    /// </summary>
    public TimeSpan SampleInterval { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>
    /// Number of recent samples kept for the regression. The window spans WindowSize * SampleInterval, so
    /// the estimate reacts to current conditions rather than the whole-crawl average.
    /// </summary>
    public int WindowSize { get; set; } = 30;

    /// <summary>How often a progress line is emitted to the log.</summary>
    public TimeSpan LogInterval { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Suppress any projection until at least this many pages are done, to skip the noisy warm-up.
    /// </summary>
    public int MinPagesBeforeEstimate { get; set; } = 20;

    /// <summary>
    /// Once the frontier starts contracting, how long that must hold continuously before an ETA is
    /// trusted. Guards against a brief dip in discovery yielding a confident estimate that a later burst
    /// of new links overturns. A single window seeing yield &lt; 1 is not enough; the drain must persist.
    /// </summary>
    public TimeSpan SustainedDrain { get; set; } = TimeSpan.FromSeconds(60);
}
