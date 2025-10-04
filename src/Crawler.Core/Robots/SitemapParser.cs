// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Linq;

namespace Crawler.Core.Robots;

/// <summary>
/// Parses a <see cref="Sitemap"/> XML document
/// </summary>
public class SitemapParser
{
    private static readonly XNamespace _sitemapNamespace = "http://www.sitemaps.org/schemas/sitemap/0.9";

    /// <summary>
    /// Parses a <see cref="Sitemap"/> from a <see cref="Stream"/>
    /// </summary>
    /// <param name="stream">Sitemap document stream</param>
    /// <param name="modifiedSince">Filters the sitemap on the modified date</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The parsed <see cref="Sitemap"/></returns>
    /// <exception cref="SitemapException">Raised when there is an error parsing the Sitemap</exception>
    public static async Task<Sitemap> ReadFromStreamAsync(Stream stream, DateTime? modifiedSince = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var reader = XmlReader.Create(stream, new XmlReaderSettings { Async = true });
            await reader.MoveToContentAsync();

            return reader switch
            {
                XmlReader when reader.NamespaceURI == _sitemapNamespace && reader.Name == "urlset"
                    => new Sitemap(ParseUrlSet(reader, modifiedSince, cancellationToken)),

                XmlReader when reader.NamespaceURI == _sitemapNamespace && reader.Name == "sitemapindex"
                    => new SitemapIndex(ParseSitemapIndex(reader, modifiedSince, cancellationToken)),

                _ => throw new SitemapException("Unable to find root sitemap element")
            };
        }
        catch (Exception e) when (e is not SitemapException)
        {
            throw new SitemapException("Unable to parse sitemap", e);
        }
    }

    private static async IAsyncEnumerable<Uri> ParseSitemapIndex(XmlReader reader, DateTime? modifiedSince, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            await reader.ReadAsync();

            while (!reader.EOF && reader.ReadState is ReadState.Interactive && !cancellationToken.IsCancellationRequested)
            {
                if (reader.NodeType is not XmlNodeType.Element || reader.Name != "sitemap" || reader.NamespaceURI != _sitemapNamespace)
                {
                    await reader.ReadAsync();
                    continue;
                }

                XElement node;
                try
                {
                    node = (XElement)await XNode.ReadFromAsync(reader, cancellationToken);
                }
                catch (Exception e)
                {
                    throw new SitemapException("Unable to parse sitemap item", e);
                }

                Uri location;
                try
                {
                    var lastModifiedString = node.Element(_sitemapNamespace + "lastmod")?.Value;
                    DateTime? lastModified = lastModifiedString is not null ? DateTime.Parse(lastModifiedString) : null;
                    if (modifiedSince is not null && lastModified is not null && lastModified < modifiedSince) continue;
                    location = new Uri(node.Element(_sitemapNamespace + "loc")!.Value);
                }
                catch (Exception e)
                {
                    throw new SitemapException("Unable to parse sitemap item", e);
                }

                yield return location;
            }
        }
        finally
        {
            reader.Dispose();
        }
    }

    private static async IAsyncEnumerable<UrlSetItem> ParseUrlSet(XmlReader reader, DateTime? modifiedSince, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        try
        {
            await reader.ReadAsync();

            while (!reader.EOF && reader.ReadState is ReadState.Interactive && !cancellationToken.IsCancellationRequested)
            {
                if (reader.NodeType is not XmlNodeType.Element || reader.Name != "url" || reader.NamespaceURI != _sitemapNamespace)
                {
                    await reader.ReadAsync();
                    continue;
                }

                XElement node;
                try
                {
                    node = (XElement)await XNode.ReadFromAsync(reader, cancellationToken);
                }
                catch (Exception e)
                {
                    throw new SitemapException("Unable to parse sitemap item", e);
                }

                Uri location;
                DateTime? lastModified;
                ChangeFrequency? changeFrequency;
                decimal? priority;

                try
                {
                    var lastModifiedString = node.Element(_sitemapNamespace + "lastmod")?.Value;
                    lastModified = lastModifiedString is not null ? DateTime.Parse(lastModifiedString) : null;

                    if (modifiedSince is not null && lastModified is not null && lastModified < modifiedSince) continue;

                    location = new Uri(node.Element(_sitemapNamespace + "loc")!.Value, UriKind.RelativeOrAbsolute);

                    var changeFrequencyString = node.Element(_sitemapNamespace + "changefreq")?.Value;
                    var priorityString = node.Element(_sitemapNamespace + "priority")?.Value;
                    changeFrequency = changeFrequencyString is not null
                        ? Enum.Parse<ChangeFrequency>(changeFrequencyString, ignoreCase: true)
                        : null;
                    priority = priorityString is not null ? decimal.Parse(priorityString) : null;
                }
                catch (Exception e)
                {
                    throw new SitemapException("Unable to parse sitemap item", e);
                }

                yield return new UrlSetItem(location, lastModified, changeFrequency, priority);
            }
        }
        finally
        {
            reader.Dispose();
        }
    }
}
