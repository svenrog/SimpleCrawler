using System.Collections.Concurrent;

namespace SimpleCrawler.Extensions;

public static class DictionaryExtensions
{
    public static void AddKeyRange<T, U>(this ConcurrentDictionary<T, U> dictionary, IEnumerable<T> keys, U value)
        where T : notnull
    {
        foreach (var key in keys)
        {
            dictionary.TryAdd(key, value);
        }
    }
}
