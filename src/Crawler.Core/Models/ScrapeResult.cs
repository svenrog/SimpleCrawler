namespace Crawler.Core.Models;

public class ScrapeResult : IScrapeResult
{
    public required IReadOnlyCollection<string> Urls { get; set; }
}
