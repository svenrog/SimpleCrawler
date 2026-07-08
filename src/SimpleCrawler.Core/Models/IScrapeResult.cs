namespace SimpleCrawler.Core.Models;

public interface IScrapeResult
{
    IReadOnlyCollection<string> Urls { get; }
    IReadOnlyCollection<UrlReport> Reports { get; }
}
