// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

using System.Text;

namespace Crawler.Core.Robots;

/// <summary>
/// Provides the ability to parse robots.txt 
/// </summary>
public class RobotsTxtParser
{
    private const long _byteCount500KiB = 500 * 1024;

    private static readonly string _userAgentDirective = "User-agent: ";
    private static readonly string _crawlDelayDirective = "Crawl-delay: ";
    private static readonly string _hostDirective = "Host: ";
    private static readonly string _sitemapDirective = "Sitemap: ";
    private static readonly string _allowDirective = "Allow: ";
    private static readonly string _disallowDirective = "Disallow: ";

    private readonly IRobotClient _robotClient;
    private readonly RobotOptions _options;

    /// <summary>
    /// Creates a robots.txt parser
    /// </summary>
    /// <param name="robotClient">Client used to send requests to the website</param>
    public RobotsTxtParser(IRobotClient robotClient, RobotOptions? options = null)
    {
        _robotClient = robotClient;
        _options = options ?? RobotOptions.Default;
    }

    /// <summary>
    /// Parses <see cref="RobotsTxt"/> from a <see cref="Stream"/>
    /// </summary>
    /// <param name="stream">The input stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Parsed <see cref="IRobotsTxt"/></returns>
    /// <exception cref="RobotsTxtException">Raised when there is an error parsing the robots.txt file</exception>
    public async Task<IRobotsTxt> ReadFromStreamAsync(Stream stream, CancellationToken cancellationToken = default)
    {
        string? line;

        string? host = null;
        var sitemaps = new HashSet<Uri>();

        var previousLineWasUserAgent = false;
        /*
          Crawlers MUST use case-insensitive matching to find the group that matches the product token
        */
        var currentUserAgents = new HashSet<ProductToken>();
        var userAgentRules = new Dictionary<ProductToken, HashSet<UrlRule>>();
        var userAgentCrawlDirectives = new Dictionary<ProductToken, int>();

        try
        {
            /*
              The file MUST be UTF-8 encoded
            */
            using var streamReader = new StreamReader(stream, Encoding.UTF8);
            while ((line = await streamReader.ReadLineAsync(cancellationToken)) is not null && !cancellationToken.IsCancellationRequested)
            {
                if (stream.Position > _byteCount500KiB) throw new RobotsTxtException("Reached parsing limit");

                if (line.StartsWith('#')) continue;

                if (line.StartsWith(_userAgentDirective, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (!previousLineWasUserAgent) currentUserAgents.Clear();
                    var currentUserAgent = GetValueOfDirective(line, _userAgentDirective);
                    if (ProductToken.TryParse(currentUserAgent, out var productToken))
                    {
                        currentUserAgents.Add(productToken);
                        userAgentRules.TryAdd(productToken, []);
                        previousLineWasUserAgent = true;
                    }
                    continue;
                }

                if (currentUserAgents.Count == 0)
                {
                    if (line.StartsWith(_sitemapDirective, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var sitemapValue = GetValueOfDirective(line, _sitemapDirective);
                        if (Uri.TryCreate(sitemapValue, UriKind.Absolute, out var sitemapAddress)) sitemaps.Add(sitemapAddress);
                    }
                    else if (host is null && line.StartsWith(_hostDirective, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var hostValue = GetValueOfDirective(line, _hostDirective);
                        if (Uri.IsWellFormedUriString(hostValue, UriKind.Absolute)
                            && Uri.TryCreate(hostValue, UriKind.Absolute, out var uri)) hostValue = uri.Host;
                        var hostNameType = Uri.CheckHostName(hostValue);
                        if (hostNameType != UriHostNameType.Unknown && hostNameType != UriHostNameType.Basic) host = hostValue;
                    }
                }
                else
                {
                    if (line.StartsWith(_disallowDirective, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var disallowValue = GetValueOfDirective(line, _disallowDirective);
                        var disallowPattern = new UrlPathPattern(disallowValue, _options.EnableRfc3986Normalization);

                        foreach (var userAgent in currentUserAgents) userAgentRules[userAgent].Add(new UrlRule(RuleType.Disallow, disallowPattern));
                    }
                    else if (line.StartsWith(_allowDirective, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var allowedValue = GetValueOfDirective(line, _allowDirective);
                        var allowPattern = new UrlPathPattern(allowedValue, _options.EnableRfc3986Normalization);

                        foreach (var userAgent in currentUserAgents) userAgentRules[userAgent].Add(new UrlRule(RuleType.Allow, allowPattern));
                    }
                    else if (line.StartsWith(_crawlDelayDirective, StringComparison.InvariantCultureIgnoreCase))
                    {
                        var crawlDelayValue = GetValueOfDirective(line, _crawlDelayDirective);
                        if (int.TryParse(crawlDelayValue, out var parsedCrawlDelay))
                        {
                            foreach (var userAgent in currentUserAgents) userAgentCrawlDirectives.TryAdd(userAgent, parsedCrawlDelay);
                        }
                    }
                }

                previousLineWasUserAgent = false;
            }

            return new RobotsTxt(_robotClient, userAgentRules, userAgentCrawlDirectives, host, sitemaps);
        }
        catch (Exception e) when (e is not RobotsTxtException)
        {
            throw new RobotsTxtException("Unable to parse robots.txt", e);
        }
    }

    private static string GetValueOfDirective(string line, string directive)
    {
        var lineWithoutDirective = line[directive.Length..];
        var endOfValueIndex = lineWithoutDirective.IndexOf(' ');
        if (endOfValueIndex == -1) endOfValueIndex = lineWithoutDirective.Length;
        return lineWithoutDirective[..endOfValueIndex];
    }
}
