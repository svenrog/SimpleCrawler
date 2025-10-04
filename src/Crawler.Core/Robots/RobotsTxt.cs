// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace Crawler.Core.Robots;

/// <summary>
/// A representation of the contained directives in a robots.txt file
/// </summary>
public class RobotsTxt : IRobotsTxt
{
    private readonly IRobotClient _client;

    private readonly IReadOnlyDictionary<ProductToken, HashSet<UrlRule>> _userAgentRules;
    private readonly IReadOnlyDictionary<ProductToken, int> _userAgentCrawlDirectives;
    private readonly HashSet<ProductToken> _userAgents;
    private readonly string? _host;
    private readonly HashSet<Uri> _sitemapUrls;

    internal RobotsTxt(IRobotClient client,
                       IReadOnlyDictionary<ProductToken, HashSet<UrlRule>> userAgentRules,
                       IReadOnlyDictionary<ProductToken, int> userAgentCrawlDirectives,
                       string? host,
                       HashSet<Uri> sitemapUrls)
    {
        _client = client;
        _userAgentRules = userAgentRules;
        _userAgentCrawlDirectives = userAgentCrawlDirectives;
        _userAgents = _userAgentRules.Keys.Concat(_userAgentCrawlDirectives.Keys).ToHashSet();
        _host = host;
        _sitemapUrls = sitemapUrls;
    }

    /// <inheritdoc />
    public async IAsyncEnumerable<UrlSetItem> LoadSitemapAsync(Uri baseAddress, DateTime? modifiedSince = default, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var urls = _sitemapUrls.Count != 0 ? _sitemapUrls.AsEnumerable() : [new Uri(baseAddress.GetLeftPart(UriPartial.Authority) + "/sitemap.xml")];

        foreach (var url in urls)
        {
            await foreach (var item in _client.LoadSitemapsAsync(url, modifiedSince, cancellationToken))
            {
                yield return item;
            }
        }
    }

    /// <inheritdoc />
    public bool TryGetCrawlDelay(ProductToken userAgent, out int crawlDelay)
    {
        var userAgentMatch = _userAgentCrawlDirectives.TryGetValue(userAgent, out crawlDelay);
        if (!userAgentMatch)
        {
            if (_userAgents.Contains(userAgent)) return false;
            return _userAgentCrawlDirectives.TryGetValue(ProductToken.Wildcard, out crawlDelay);
        }

        return true;
    }

    /// <inheritdoc />
    public bool TryGetHost([NotNullWhen(true)] out string? host)
    {
        host = _host;
        return _host is not null;
    }

    /// <inheritdoc />
    public bool TryGetRules(ProductToken userAgent, [NotNullWhen(true)] out IRobotRuleChecker? ruleChecker)
    {
        if (!_userAgentRules.TryGetValue(userAgent, out var rules) && !_userAgentRules.TryGetValue(ProductToken.Wildcard, out rules))
        {
            ruleChecker = new RobotRuleChecker([]);
            return false;
        }

        ruleChecker = new RobotRuleChecker(rules);
        return true;
    }
}
