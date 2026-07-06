namespace Crawler.Core.Proxy;

public sealed class ProxyPoolOptions
{
    public int MaxRetries { get; set; } = 3;
    public int FailureThreshold { get; set; } = 3;
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromSeconds(60);
    public double MinHealthyRatio { get; set; } = 0.25;
}
