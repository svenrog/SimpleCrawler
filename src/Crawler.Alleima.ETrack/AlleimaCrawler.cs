using Crawler.Alleima.ETrack.Models;
using Crawler.Core;
using Crawler.HtmlAgilityPack;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;

namespace Crawler.Alleima.ETrack;

public sealed class AlleimaCrawler(HttpClient client, IOptions<CrawlerOptions> options, ILogger<AlleimaCrawler> logger) : HtmlAgilityPackCrawler<AlleimaScrapeResult>(client, options, logger)
{
    private readonly ConcurrentDictionary<string, bool> _otherPage = [];
    private readonly ConcurrentDictionary<string, bool> _categoryPage = [];
    private readonly ConcurrentDictionary<string, bool> _productPage = [];
    private readonly ConcurrentDictionary<string, bool> _variationPage = [];

    protected override ValueTask AnalyzeDocument(string url, HtmlDocument document)
    {
        var productList = document.DocumentNode.SelectSingleNode("//ul[contains(@class,'product-list')]");
        if (productList != null)
        {
            _categoryPage.TryAdd(url, true);
            return ValueTask.CompletedTask;
        }

        var productHeader = document.DocumentNode.SelectSingleNode("//h1[contains(@class, 'product-header__heading')]");
        if (productHeader != null)
        {
            _productPage.TryAdd(url, true);
            return ValueTask.CompletedTask;
        }

        var dimensionHeading = document.DocumentNode.SelectSingleNode("//h2[contains(@class, 'heading-type-6')]");
        if (dimensionHeading?.InnerHtml == "Dimensions")
        {
            _variationPage.TryAdd(url, true);
            return ValueTask.CompletedTask;
        }

        _otherPage.TryAdd(url, true);
        return ValueTask.CompletedTask;
    }

    protected override ValueTask<AlleimaScrapeResult> GetResult(CancellationToken cancellationToken)
    {
        var result = new AlleimaScrapeResult
        {
            Categories = [.. _categoryPage.Keys.Order()],
            Products = [.. _productPage.Keys.Order()],
            Variations = [.. _variationPage.Keys.Order()],
            Other = [.. _otherPage.Keys.Order()],
        };

        return ValueTask.FromResult(result);
    }
}
