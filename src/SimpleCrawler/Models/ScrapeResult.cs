namespace SimpleCrawler.Models;

public class ScrapeResult
{
    public required IReadOnlyCollection<string> Urls { get; set; }
}
