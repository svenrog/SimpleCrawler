namespace SimpleCrawler.Core.Retry;

public sealed class RetryOptions
{
    /// <summary>
    /// Additional attempts after the first; total attempts = MaxRetries + 1.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Fraction of the computed delay applied as ± random jitter (0.2 = ±20%).
    /// </summary>
    public double JitterFactor { get; set; } = 0.2;

    /// <summary>
    /// Back off before retrying a rate-limited (429) response even when a fresh proxy is available,
    /// treating the limit as target-side rather than route-side.
    /// </summary>
    public bool DelayOnRateLimit { get; set; } = true;

    /// <summary>
    /// Upper bound on a single attempt; a slower attempt is cancelled and retried. Zero disables it.
    /// </summary>
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(100);
}
