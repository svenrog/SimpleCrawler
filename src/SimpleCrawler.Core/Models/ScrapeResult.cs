namespace SimpleCrawler.Core.Models;

public sealed class ScrapeResult : IScrapeResult
{
    public required IReadOnlyCollection<string> Urls { get; set; }
    public IReadOnlyCollection<UrlReport> Reports { get; set; } = [];
}
