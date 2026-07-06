namespace Crawler.Core.Extensions;

internal static class StringExtensions
{
    public static string WithoutQuery(this string input)
    {
        var queryIndex = input.IndexOf("?");
        if (queryIndex > -1)
        {
            input = input[..queryIndex];
        }

        var hashIndex = input.IndexOf("#");
        if (hashIndex > -1)
        {
            input = input[..hashIndex];
        }

        return input;
    }

    public static string AppendTrailingSlash(this string input)
    {
        if (input.EndsWith('/'))
            return input;

        return input + "/";
    }
}
