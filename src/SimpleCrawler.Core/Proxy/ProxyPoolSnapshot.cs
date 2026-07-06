namespace SimpleCrawler.Core.Proxy;

public sealed record ProxyPoolSnapshot
{
    public int Total { get; init; }
    public int Healthy { get; init; }
    public double HealthyRatio => Total == 0 ? 0 : (double)Healthy / Total;
}
