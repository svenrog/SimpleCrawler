using Crawler.Core.Collections;

namespace Crawler.Core.Extensions;

public static class SetExtensions
{
    public static void AddRange<T>(this ConcurrentHashSet<T> set, IEnumerable<T> values)
        where T : notnull
    {
        foreach (var value in values)
        {
            set.Add(value);
        }
    }
}
