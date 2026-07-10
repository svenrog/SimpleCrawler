namespace SimpleCrawler.Core.Progress;

/// <summary>
/// A snapshot of a crawl's projected completion, produced by <see cref="CrawlProgressEstimator"/> from a
/// recent window of samples. The ETA fields are only populated when <see cref="State"/> is
/// <see cref="ProgressState.Estimating"/>.
/// </summary>
public readonly record struct ProgressEstimate
{
    /// <summary>Which confidence phase the estimate is in.</summary>
    public ProgressState State { get; init; }

    /// <summary>Pages fully processed so far.</summary>
    public int Processed { get; init; }

    /// <summary>Distinct URLs discovered so far (processed plus the pending frontier).</summary>
    public int Discovered { get; init; }

    /// <summary>URLs discovered but not yet processed (the frontier, <c>Discovered - Processed</c>).</summary>
    public int Pending { get; init; }

    /// <summary>Pages processed per second, from the window (0 until measurable).</summary>
    public double Throughput { get; init; }

    /// <summary>New URLs discovered per page processed, from the window (the decaying yield g).</summary>
    public double Yield { get; init; }

    /// <summary>Expected pages still to process. Only meaningful when <see cref="ProgressState.Estimating"/>.</summary>
    public double RemainingPages { get; init; }

    /// <summary>Projected grand total of pages. Only meaningful when <see cref="ProgressState.Estimating"/>.</summary>
    public double EstimatedTotalPages { get; init; }

    /// <summary>Optimistic end of the ETA band (uses the optimistic yield); null unless estimating.</summary>
    public TimeSpan? EtaLow { get; init; }

    /// <summary>Pessimistic end of the ETA band (uses the pessimistic yield); null unless estimating.</summary>
    public TimeSpan? EtaHigh { get; init; }
}
