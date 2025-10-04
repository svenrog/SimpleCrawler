// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

using System.Net.Mime;
using System.Runtime.CompilerServices;

namespace Crawler.Core.Robots.Http;

/// <summary>
/// Client for retrieving robots.txt from a website
/// </summary>
public class RobotWebClient : IRobotClient
{
    private readonly HttpClient _httpClient;

    public RobotWebClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IRobotsTxt> LoadRobotsTxtAsync(Uri url, CancellationToken cancellationToken = default)
    {
        /*
           "The instructions must be accessible via HTTP [2] from the site that the instructions are to be applied to, as a resource of Internet
           Media Type [3] "text/plain" under a standard relative path on the server: "/robots.txt"."
        */
        var request = new HttpRequestMessage(HttpMethod.Get, url.GetLeftPart(UriPartial.Authority) + "/robots.txt");
        request.Headers.Add("Accept", "text/plain,*/*");

        var response = await _httpClient.SendAsync(request, cancellationToken);
        var statusCodeNumber = (int)response.StatusCode;

        if (statusCodeNumber >= 400 && statusCodeNumber <= 499)
        {
            /*
                "Unavailable" means the crawler tries to fetch the robots.txt file and the server responds with status codes indicating that
                the resource in question is unavailable. For example, in the context of HTTP, such status codes are in the 400-499 range.

                If a server status code indicates that the robots.txt file is unavailable to the crawler,
                then the crawler MAY access any resources on the server.
            */
            return new RobotsTxt(this, new Dictionary<ProductToken, HashSet<UrlRule>>(), new Dictionary<ProductToken, int>(), null, new HashSet<Uri>());
        }

        if (statusCodeNumber >= 500)
        {
            /*
                If the robots.txt file is unreachable due to server or network errors, this means the robots.txt file is undefined and the
                crawler MUST assume complete disallow. For example, in the context of HTTP, server errors are identified by status codes in
                the 500-599 range.
            */
            var userAgentRules = new Dictionary<ProductToken, HashSet<UrlRule>>
            {
                { ProductToken.Wildcard, new HashSet<UrlRule> { new (RuleType.Disallow, "/") } }
            };
            return new RobotsTxt(this, userAgentRules, new Dictionary<ProductToken, int>(), null, new HashSet<Uri>());
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await new RobotsTxtParser(this).ReadFromStreamAsync(stream, cancellationToken);
    }

    public async IAsyncEnumerable<UrlSetItem> LoadSitemapsAsync(Uri uri, DateTime? modifiedSince, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("Accept", "application/xml,text/plain,text/xml,*/*");

        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode) yield break;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        var headers = response.Content.Headers;
        var mediaType = headers.ContentType?.MediaType;

        if (mediaType == MediaTypeNames.Text.Plain)
        {
            await foreach (var urlSet in SimpleTextSitemapParser.ReadFromStreamAsync(stream, cancellationToken))
            {
                yield return urlSet;
            }

            yield break;
        }

        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, modifiedSince, cancellationToken);
        if (sitemap is SitemapIndex index)
        {
            await foreach (var location in index.SitemapUris)
            {
                await foreach (var item in LoadSitemapsAsync(location, modifiedSince, cancellationToken))
                {
                    yield return item;
                }
            }
        }
        else
        {
            await foreach (var item in sitemap.UrlSet)
            {
                yield return item;
            }
        }
    }
}
