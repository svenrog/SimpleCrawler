using Crawler.Core.Comparers;
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

    internal static bool IsUrlEncoded(string value)
    {
        var index = value.IndexOf('%');

        while (index >= 0)
        {
            var valueSpan = value.AsSpan();
            var slice = valueSpan[index..Math.Min(index + 3, value.Length)];

            if (!slice.SequenceEqual(_urlEncodedSlash, CharComparer.InvariantCultureIgnoreCase))
                return true;

            index = value.IndexOf('%', index + 1);
        }

        return false;
    }
}
