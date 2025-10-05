using Crawler.Core.Comparers;
using Crawler.Core.Extensions;
using System.Numerics;
using System.Web;

namespace Crawler.Core.Robots;

internal static class PathHelpers
{
    private const string _urlEncodedSlash = "%2f";

    internal static string ConvertToUtf16ForComparison(string value)
    {
        if (!IsUrlEncoded(value))
            return value;

        return HttpUtility.UrlDecode(value);
    }

    internal static bool IsUrlEncoded(ReadOnlySpan<char> value)
    {
        var index = value.IndexOf('%');

        while (index >= 0)
        {
            var slice = value[index..Math.Min(index + 3, value.Length)];

            if (!slice.SequenceEqual(_urlEncodedSlash, CharComparer.InvariantCultureIgnoreCase))
                return true;

            index = value.IndexOf('%', index + 1);
        }

        return false;
    }
}
