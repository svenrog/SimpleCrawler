namespace Crawler.Core.Extensions;

public static class CollectionExtensions
{
    public static bool Contains(this ICollection<string> collection, ReadOnlySpan<char> value, IEqualityComparer<char>? comparer = null)
    {
        foreach (var item in collection)
        {
            if (value.SequenceEqual(item, comparer))
                return true;
        }

        return false;
    }

}
