using Crawler.Alleima.ETrack.Models;
using Crawler.Core;
using Crawler.Core.Models;
using HtmlAgilityPack;
using HtmlAgilityPack.CssSelectors.NetCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Crawler.Alleima.ETrack;

public class AlleimaCrawler(HtmlWeb client, IOptions<CrawlerOptions> options, ILogger<AlleimaCrawler> logger) : CrawlerBase(client, options, logger)
{
    private readonly ConcurrentDictionary<string, bool> _otherPage = [];
    private readonly ConcurrentDictionary<string, bool> _categoryPage = [];
    private readonly ConcurrentDictionary<string, bool> _productPage = [];
    private readonly ConcurrentDictionary<string, bool> _variationPage = [];

    protected override Task<HtmlDocument> LoadUrl(HtmlWeb client, string url, CancellationToken cancellationToken)
    {
        //TODO: Rewrite entire HTMLAgilityPack request handling
        //Currently unable to add headers

        return client.LoadFromWebAsync(url, cancellationToken);
    }

    protected override ValueTask AnalyzeDocument(string url, HtmlDocument document)
    {
        var productList = document.DocumentNode.QuerySelector("ul.product-list");
        if (productList != null)
        {
            _categoryPage.TryAdd(url, true);
            return ValueTask.CompletedTask;
        }

        var productHeader = document.DocumentNode.QuerySelector("h1.product-header__heading");
        if (productHeader != null)
        {
            _productPage.TryAdd(url, true);
            return ValueTask.CompletedTask;
        }

        var dimensionHeading = document.DocumentNode.QuerySelector("h2.heading-type-6");
        if (dimensionHeading?.InnerHtml == "Dimensions")
        {
            _variationPage.TryAdd(url, true);
            return ValueTask.CompletedTask;
        }

        _otherPage.TryAdd(url, true);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<IScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        var result = new AlleimaScrapeResult
        {
            Categories = [.. _categoryPage.Keys.Order()],
            Products = [.. _productPage.Keys.Order()],
            Variations = [.. _variationPage.Keys.Order()],
            Other = [.. _otherPage.Keys.Order()],
        };

        return ValueTask.FromResult<IScrapeResult>(result);
    }
}
