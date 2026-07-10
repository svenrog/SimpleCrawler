namespace SimpleCrawler.Core.Progress;

/// <summary>
/// The confidence phase a crawl's time estimate is in, progressing from too-little-data through frontier
/// expansion and contraction to a trusted estimate.
/// </summary>
public enum ProgressState
{
    /// <summary>Too little data yet (crawl just started, or no forward progress in the window).</summary>
    WarmingUp,

    /// <summary>
    /// Each processed page still yields more than one new URL on average, so the frontier is growing and
    /// no finish time can be projected.
    /// </summary>
    Expanding,

    /// <summary>
    /// The frontier has started contracting, but not yet for long enough to trust: a later burst of new
    /// links could still overturn it, so an ETA is withheld.
    /// </summary>
    Draining,

    /// <summary>
    /// The frontier has been contracting long enough to trust; a projected total and ETA are available.
    /// </summary>
    Estimating,
}
