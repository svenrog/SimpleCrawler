using System.Net.Mime;
using System.Runtime.CompilerServices;

namespace SimpleCrawler.Core.Robots;

public abstract class AbstractBrowserRobotClient : IRobotClient
{
    public async Task<IRobotsTxt> LoadRobotsTxtAsync(Uri url, CancellationToken cancellationToken = default)
    {
        var robotsUrl = url.GetLeftPart(UriPartial.Authority) + "/robots.txt";
        var response = await FetchAsync(robotsUrl, cancellationToken);

        if (response.Status is >= 400 and <= 499)
            return new RobotsTxt(this, new Dictionary<ProductToken, HashSet<UrlRule>>(), new Dictionary<ProductToken, int>(), null, []);

        if (response.Status >= 500 || response.Body is null)
        {
            var disallowAll = new Dictionary<ProductToken, HashSet<UrlRule>>
            {
                { ProductToken.Wildcard, new HashSet<UrlRule> { new(RuleType.Disallow, new UrlPathPattern("/")) } }
            };
            return new RobotsTxt(this, disallowAll, new Dictionary<ProductToken, int>(), null, []);
        }

        using var stream = new MemoryStream(response.Body);
        return await new RobotsTxtParser(this).ReadFromStreamAsync(stream, cancellationToken);
    }

    public async IAsyncEnumerable<UrlSetItem> LoadSitemapsAsync(Uri uri, DateTime? modifiedSince = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await FetchAsync(uri.ToString(), cancellationToken);
        if (response.Status is < 200 or >= 300 || response.Body is null)
            yield break;

        using var stream = new MemoryStream(response.Body);

        if (response.MediaType == MediaTypeNames.Text.Plain)
        {
            await foreach (var item in SimpleTextSitemapParser.ReadFromStreamAsync(stream, cancellationToken))
                yield return item;

            yield break;
        }

        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, modifiedSince, cancellationToken);
        if (sitemap is SitemapIndex index)
        {
            await foreach (var location in index.SitemapUris)
            {
                await foreach (var item in LoadSitemapsAsync(location, modifiedSince, cancellationToken))
                    yield return item;
            }
        }
        else
        {
            await foreach (var item in sitemap.UrlSet)
                yield return item;
        }
    }

    protected abstract Task<RobotResourceResponse> FetchAsync(string url, CancellationToken cancellationToken);

    protected static string? ParseMediaType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
            return null;

        var separator = contentType.IndexOf(';');
        return (separator >= 0 ? contentType[..separator] : contentType).Trim();
    }
}
