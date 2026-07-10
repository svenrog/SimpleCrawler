namespace SimpleCrawler.Core.Throttling;

public sealed class ThrottleOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Ceiling, in seconds, on the total effective delay (base + rate-limit penalty) for a host.
    /// </summary>
    public double MaxDelaySeconds { get; set; } = 60;
}
