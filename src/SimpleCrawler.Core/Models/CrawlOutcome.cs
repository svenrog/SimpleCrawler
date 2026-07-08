namespace SimpleCrawler.Core.Models;

public enum CrawlOutcome
{
    Success,
    HttpError,
    Timeout,
    FetchError,
    ParseError,
    RetriesExhausted,
    Aborted,
}
