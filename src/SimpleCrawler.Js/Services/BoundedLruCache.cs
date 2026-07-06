namespace Crawler.Js.Services;

// The render caches assume a single-domain SPA serving the same bundle on every route, so keying parsed
// scripts/modules/sources by URL for the whole crawl is bounded and a large win. A heterogeneous site
// (thousands of distinct, content-hashed chunks) breaks that assumption and the caches grow without
// bound, so every entry is held under a capacity cap with least-recently-used eviction: the hot shared
// bundle stays resident while one-off per-page sources churn through and are reclaimed.
public sealed class BoundedLruCache<TKey, TValue>
    where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<Entry>> _map;
    private readonly LinkedList<Entry> _recency = new();
    private readonly Lock _gate = new();

    public BoundedLruCache(int capacity)
    {
        _capacity = capacity;
        _map = new Dictionary<TKey, LinkedListNode<Entry>>(capacity);
    }

    public bool TryGet(TKey key, out TValue value)
    {
        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                Touch(node);
                value = node.Value.Value;
                return true;
            }
        }

        value = default!;
        return false;
    }

    public void Set(TKey key, TValue value)
    {
        lock (_gate)
        {
            Store(key, value);
        }
    }

    // The factory runs outside the lock (parsing/fetching is the cost the cache exists to avoid serializing);
    // a rare concurrent miss may compute twice, and the first stored value wins, matching ConcurrentDictionary.
    public TValue GetOrAdd<TArg>(TKey key, TArg arg, Func<TKey, TArg, TValue> factory)
    {
        if (TryGet(key, out var existing))
            return existing;

        var created = factory(key, arg);

        lock (_gate)
        {
            if (_map.TryGetValue(key, out var node))
            {
                Touch(node);
                return node.Value.Value;
            }

            Store(key, created);
            return created;
        }
    }

    private void Store(TKey key, TValue value)
    {
        if (_map.TryGetValue(key, out var existing))
        {
            existing.Value = new Entry(key, value);
            Touch(existing);
            return;
        }

        var node = _recency.AddFirst(new Entry(key, value));
        _map[key] = node;

        if (_map.Count > _capacity)
        {
            var oldest = _recency.Last!;
            _recency.RemoveLast();
            _map.Remove(oldest.Value.Key);
        }
    }

    private void Touch(LinkedListNode<Entry> node)
    {
        if (node.List is not null)
            _recency.Remove(node);

        _recency.AddFirst(node);
    }

    private readonly record struct Entry(TKey Key, TValue Value);
}
