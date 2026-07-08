using SimpleCrawler.Core.Collections;
using SimpleCrawler.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace SimpleCrawler.Core.Checkpoints;

// The full resumable state of a crawl, owned as a single object by the crawler and handed to the
// checkpoint store whole. The sets are the live, thread-safe collections the crawl mutates as it runs;
// a checkpoint is just a serialized snapshot of this object.
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

    // Every URL ever enqueued; prevents re-discovery.
    [JsonConverter(typeof(ConcurrentHashSetJsonConverter))]
    public ConcurrentHashSet<string> Discovered { get; set; } = [];

    // URLs whose fetch+parse has completed or permanently failed; the pending frontier is
    // Discovered minus Processed.
    [JsonConverter(typeof(ConcurrentHashSetJsonConverter))]
    public ConcurrentHashSet<string> Processed { get; set; } = [];

    // Indexable URLs that make up the crawl result.
    [JsonConverter(typeof(ConcurrentHashSetJsonConverter))]
    public ConcurrentHashSet<string> Visited { get; set; } = [];

    // Per-URL report for every fetched page, keyed by requested URL. Checkpointed alongside the sets
    // so a resumed crawl's report stays consistent with Visited rather than losing pre-checkpoint pages.
    public ConcurrentDictionary<string, UrlReport> Reports { get; set; } = new();
}
