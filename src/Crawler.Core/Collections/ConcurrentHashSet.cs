using System.Collections;
using System.Collections.Concurrent;

namespace Crawler.Core.Collections;

public class ConcurrentHashSet<T> : ICollection<T>, ISet<T>, IReadOnlyCollection<T>, IReadOnlySet<T>
    where T : notnull
{
    private readonly ConcurrentDictionary<T, bool> _dictionary;
    public ConcurrentHashSet()
    {
        _dictionary = new ConcurrentDictionary<T, bool>();
    }

    public ConcurrentHashSet(IEnumerable<T> collection)
    {
        var valuePairs = collection.Select(v => new KeyValuePair<T, bool>(v, false));
        _dictionary = new ConcurrentDictionary<T, bool>(valuePairs);
    }

    public ConcurrentHashSet(IEqualityComparer<T> equalityComparer)
    {
        _dictionary = new ConcurrentDictionary<T, bool>(equalityComparer);
    }

    public ConcurrentHashSet(int concurrencyLevel, int capacity)
    {
        _dictionary = new ConcurrentDictionary<T, bool>(concurrencyLevel, capacity);
    }

    public ConcurrentHashSet(IEnumerable<T> collection, IEqualityComparer<T> equalityComparer)
    {
        var valuePairs = collection.Select(v => new KeyValuePair<T, bool>(v, false));
        _dictionary = new ConcurrentDictionary<T, bool>(valuePairs, equalityComparer);
    }

    public ConcurrentHashSet(int concurrencyLevel, int capacity, IEqualityComparer<T> equalityComparer)
    {
        _dictionary = new ConcurrentDictionary<T, bool>(concurrencyLevel, capacity, equalityComparer);
    }

    public ConcurrentHashSet(int concurrencyLevel, IEnumerable<T> collection, IEqualityComparer<T> equalityComparer)
    {
        var valuePairs = collection.Select(v => new KeyValuePair<T, bool>(v, false));
        _dictionary = new ConcurrentDictionary<T, bool>(concurrencyLevel, valuePairs, equalityComparer);
    }

    public int Count => _dictionary.Count;

    public bool IsEmpty => _dictionary.IsEmpty;

    public bool IsReadOnly => false;

    public bool Add(T item)
    {
        return _dictionary.TryAdd(item, false);
    }

    public void Clear()
    {
        _dictionary.Clear();
    }

    public bool Contains(T item)
    {
        return _dictionary.ContainsKey(item);
    }

    public void CopyTo(T[] array, int arrayIndex)
    {
        _dictionary.Keys.CopyTo(array, arrayIndex);
    }

    public void ExceptWith(IEnumerable<T> other)
    {
        throw new NotSupportedException();
    }

    public IEnumerator<T> GetEnumerator()
    {
        return _dictionary.Keys.GetEnumerator();
    }

    public void IntersectWith(IEnumerable<T> other)
    {
        throw new NotSupportedException();
    }

    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        throw new NotSupportedException();
    }

    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        throw new NotSupportedException();
    }

    public bool IsSubsetOf(IEnumerable<T> other)
    {
        throw new NotSupportedException();
    }

    public bool IsSupersetOf(IEnumerable<T> other)
    {
        throw new NotSupportedException();
    }

    public bool Overlaps(IEnumerable<T> other)
    {
        throw new NotSupportedException();
    }

    public bool Remove(T item)
    {
        return _dictionary.TryRemove(item, out _);
    }

    public bool SetEquals(IEnumerable<T> other)
    {
        throw new NotSupportedException();
    }

    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        throw new NotSupportedException();
    }

    public void UnionWith(IEnumerable<T> other)
    {
        throw new NotSupportedException();
    }

    void ICollection<T>.Add(T item)
    {
        _dictionary.TryAdd(item, false);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _dictionary.Keys.GetEnumerator();
    }
}
