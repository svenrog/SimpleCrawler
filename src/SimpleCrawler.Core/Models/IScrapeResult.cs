namespace Crawler.Core.Models;

public interface IScrapeResult
{
    IReadOnlyCollection<string> Urls { get; }
}
