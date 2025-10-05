namespace Crawler.Core.Extensions;

internal static class ReadOnlySpanExtensions
{
    public static int IndexOf(this ReadOnlySpan<char> span, char input, int startIndex)
    {
        var indexInSlice = span[startIndex..].IndexOf(input);
        if (indexInSlice == -1)
        {
            return -1;
        }

        return startIndex + indexInSlice;
    }
}
