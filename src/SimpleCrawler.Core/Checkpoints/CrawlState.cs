using SimpleCrawler.Core.Collections;
using SimpleCrawler.Core.Models;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace SimpleCrawler.Core.Checkpoints;

/// <summary>
/// The full resumable state of a crawl, owned as a single object by the crawler and handed to the
/// checkpoint store whole. The sets are the live, thread-safe collections the crawl mutates as it runs;
/// a checkpoint is just a serialized snapshot of this object.
///
/// The dedup set <see cref="Discovered"/> is kept in memory only: a large crawl's corpus need not be
/// written to every checkpoint, so it is left unserialized and rebuilt on load from Processed plus the
/// still-pending Frontier. What is serialized is only the pending frontier (with per-URL depth), which
/// empties out as the crawl completes.
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
    /// Every URL ever enqueued; prevents re-discovery. In-memory only - rebuilt on load from
    /// <see cref="Processed"/> and <see cref="Frontier"/> rather than serialized, so the full corpus is
    /// never written to a checkpoint.
    /// </summary>
    [JsonIgnore]
    public ConcurrentHashSet<string> Discovered { get; set; } = [];

    /// <summary>
    /// The pending frontier: every URL that has entered the frontier but not yet been processed, mapped to
    /// its link depth. Entries are pruned as URLs are processed, so a completed crawl serializes an empty
    /// map. This is the only place per-URL depth is persisted.
    /// </summary>
    public ConcurrentDictionary<string, int> Frontier { get; set; } = new();

    /// <summary>
    /// URLs whose fetch+parse has completed or permanently failed. Combined with <see cref="Frontier"/> it
    /// reconstitutes <see cref="Discovered"/> on load.
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

    /// <summary>
    /// Populates the in-memory <see cref="Discovered"/> dedup set from the serialized state after a
    /// checkpoint load: the union of already-processed URLs and the still-pending frontier.
    /// </summary>
    public void RebuildAfterLoad()
    {
        Discovered = [];
        Discovered.UnionWith(Processed);
        Discovered.UnionWith(Frontier.Keys);
    }
}
