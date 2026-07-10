using SimpleCrawler.Core.Collections;
using SimpleCrawler.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace SimpleCrawler.Core.Checkpoints;

/// <summary>
/// The full resumable state of a crawl, owned as a single object by the crawler and handed to the
/// checkpoint store whole. The sets are the live, thread-safe collections the crawl mutates as it runs;
/// a checkpoint is just a serialized snapshot of this object.
/// </summary>
public sealed class CrawlState
{
    public CrawlState()
    {
    }

    public CrawlState(IReadOnlyList<string> entries)
    {
        Entries = entries;
    }

    public IReadOnlyList<string> Entries { get; set; } = [];

    /// <summary>
    /// Every URL ever enqueued; prevents re-discovery.
    /// </summary>
    [JsonConverter(typeof(ConcurrentHashSetJsonConverter))]
    public ConcurrentHashSet<string> Discovered { get; set; } = [];

    /// <summary>
    /// URLs whose fetch+parse has completed or permanently failed; the pending frontier is
    /// Discovered minus Processed.
    /// </summary>
    [JsonConverter(typeof(ConcurrentHashSetJsonConverter))]
    public ConcurrentHashSet<string> Processed { get; set; } = [];

    /// <summary>
    /// Indexable URLs that make up the crawl result.
    /// </summary>
    [JsonConverter(typeof(ConcurrentHashSetJsonConverter))]
    public ConcurrentHashSet<string> Visited { get; set; } = [];

    /// <summary>
    /// Per-URL report for every fetched page, keyed by requested URL. Checkpointed alongside the sets
    /// so a resumed crawl's report stays consistent with Visited rather than losing pre-checkpoint pages.
    /// </summary>
    public ConcurrentDictionary<string, UrlReport> Reports { get; set; } = new();
}
