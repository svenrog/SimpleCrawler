namespace SimpleCrawler.Core.Models;

/// <summary>
/// Backend-neutral, normalized view of a page's HTTP response, produced once per fetched URL and
/// handed to every registered <see cref="Collectors.ICrawlCollector"/>. Static and JS backends build
/// it from an <see cref="HttpResponseMessage"/>; headless backends from the browser's response object.
/// <see cref="Headers"/>/<see cref="CookieNames"/> are populated only when a collector is registered,
/// so a plain crawl pays for nothing beyond status/size/type.
/// </summary>
public sealed class ResponseSignal
{
    /// <summary>Shared empty header map, so the no-collector path allocates nothing.</summary>
    public static readonly IReadOnlyDictionary<string, string> EmptyHeaders = new Dictionary<string, string>();

    /// <summary>
    /// Delimiter joining the values of a header that appears more than once, so <see cref="Headers"/>
    /// reads the same across every backend. A newline, not <c>", "</c>: <c>Set-Cookie</c> values embed
    /// commas (in <c>Expires</c> dates) and must never be comma-joined, and it matches how the headless
    /// browsers' CDP already concatenates repeated headers.
    /// </summary>
    public const string HeaderValueSeparator = "\n";

    public required int StatusCode { get; init; }
    public long? ContentLength { get; init; }
    public string? ContentType { get; init; }

    /// <summary>Response headers, lower-cased keys to single joined values; empty unless captured.</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = EmptyHeaders;

    /// <summary>Names of cookies set via <c>Set-Cookie</c> (values dropped); empty unless captured.</summary>
    public IReadOnlyList<string> CookieNames { get; init; } = [];
}
