using Crawler.Core.Helpers;
using Crawler.Tests.Models;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Crawler.Tests.Assertions;

internal partial class LinkAssertions
{
    [GeneratedRegex("<a[^<]*?href=\"([^\"]*?)\"[^>]*?>(.*?)<\\/a>", RegexOptions.None, 1000)]
    private static partial Regex LinkFilterRegex();
    private static readonly Regex _linkFilterRegex = LinkFilterRegex();

    public static List<string> GetHtmlLinks(Uri baseUri, string html)
    {
        var matches = _linkFilterRegex.Matches(html);
        var links = new List<string>(matches.Count);

        foreach (Match match in matches)
        {
            var href = match.Groups[1].Value;
            var link = UriHelper.GetAbsoluteUrl(baseUri, href);
            if (link == null)
                continue;

            links.Add(link);
        }

        return links;
    }

    public static List<string> GetJsonLinks(Uri baseUri, string json)
    {
        var manifest = JsonSerializer.Deserialize<List<LinkModel>>(json);
        if (manifest == null)
            return [];

        var links = new List<string>(manifest.Count);
        foreach (var model in manifest)
        {
            var link = UriHelper.GetAbsoluteUrl(baseUri, model.Href);
            if (link == null)
                continue;

            links.Add(link);
        }

        return links;
    }
}
