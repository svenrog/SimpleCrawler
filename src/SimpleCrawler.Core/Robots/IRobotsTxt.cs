// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

using System.Diagnostics.CodeAnalysis;

namespace Crawler.Core.Robots;

/// <summary>
/// A representation of the contained directives in a robots.txt file
/// </summary>
public interface IRobotsTxt
{
    /// <summary>
    /// Retrieves the sitemap
    /// </summary>
    /// <param name="modifiedSince">Filter to retrieve site maps modified after this date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A sitemap, or null or no sitemap is found</returns>
    IAsyncEnumerable<UrlSetItem> LoadSitemapAsync(Uri url, DateTime? modifiedSince = default, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves the crawl delay specified for a User-Agent
    /// </summary>
    /// <param name="userAgent">User-Agent header to retrieve rules for</param>
    /// <param name="crawlDelay">The crawl delay in seconds</param>
    /// <returns>True if a crawl delay directive exists; otherwise false</returns>
    bool TryGetCrawlDelay(ProductToken userAgent, out int crawlDelay);

    /// <summary>
    /// Retrieves the website host
    /// </summary>
    /// <param name="host">The website host address</param>
    /// <returns>True if the host directive exists; otherwise false</returns>
    bool TryGetHost([NotNullWhen(true)] out string? host);

    /// <summary>
    /// Retrieves rules which apply to a User-Agent
    /// </summary>
    /// <param name="userAgent">User-Agent header to retrieve rules for</param>
    /// <param name="ruleChecker">A rule checker for the User-Agent</param>
    /// <returns>True if any rules are found; otherwise false</returns>

    bool TryGetRules(ProductToken userAgent, [NotNullWhen(true)] out IRobotRuleChecker? ruleChecker);
}
