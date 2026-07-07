namespace SimpleCrawler.Core.Retry;

public sealed class RetryOptions
{
    // Additional attempts after the first; total attempts = MaxRetries + 1.
    public int MaxRetries { get; set; } = 3;

    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromMilliseconds(500);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);

    // Fraction of the computed delay applied as ± random jitter (0.2 = ±20%).
    public double JitterFactor { get; set; } = 0.2;

    // Back off before retrying a rate-limited (429) response even when a fresh proxy is available,
    // treating the limit as target-side rather than route-side.
    public bool DelayOnRateLimit { get; set; } = true;

    // Upper bound on a single attempt; a slower attempt is cancelled and retried. Zero disables it.
    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(100);
}
