namespace SimpleCrawler.Core.Models;

// The live report for a single fetched URL. It is the value stored in CrawlState.Reports and is
// mutated in place as the URL moves through the fetch and parse stages (one worker at a time), so
// the report is part of the checkpointable state rather than a copy assembled on the side.
public sealed class UrlReport
{
    public required string Url { get; set; }
    public string? CanonicalUrl { get; set; }
    public int? StatusCode { get; set; }
    public CrawlOutcome Outcome { get; set; }
    public TimeSpan FetchDuration { get; set; }
    public TimeSpan? ParseDuration { get; set; }
    public long? ContentLength { get; set; }
    public string? ContentType { get; set; }
    public int LinkCount { get; set; }
    public bool Indexed { get; set; }
    public bool Followed { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string? Error { get; set; }
}
