// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

using System.Runtime.CompilerServices;

namespace Crawler.Core.Robots;

/// <summary>
/// Parses a <see cref="Sitemap"/> TXT document
/// </summary>
public static class SimpleTextSitemapParser
{
    private const int _maxLines = 50000;
    private const int _byteCount50MiB = 52_428_800;

    /// <summary>
    /// Parses a <see cref="Sitemap"/> from a <see cref="Stream"/>
    /// </summary>
    /// <param name="stream">Sitemap document stream</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The parsed <see cref="Sitemap"/></returns>
    /// <exception cref="SitemapException">Raised when there is an error parsing the Sitemap</exception>
    public static async IAsyncEnumerable<UrlSetItem> ReadFromStreamAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var streamReader = new StreamReader(stream);
        string? line;
        var lineCount = 0;
        while (((line = await streamReader.ReadLineAsync(cancellationToken)) is not null) && !cancellationToken.IsCancellationRequested)
        {
            /*
              Each text file ... and must be no larger than 50MiB (52,428,800 bytes)
            */
            if (stream.Position > _byteCount50MiB) throw new SitemapException("Reached parsing limit");

            if (string.IsNullOrWhiteSpace(line)) continue;

            lineCount++;

            /*
              Each text file can contain a maximum of 50,000 URLs
            */
            if (lineCount > _maxLines) throw new SitemapException("Reached line limit");

            /*
              The text file must have one URL per line. The URLs cannot contain embedded new lines.
              You must fully specify URLs, including the http.
              The text file must use UTF-8 encoding.
              The text file should contain no information other than the list of URLs.
              The text file should contain no header or footer information.
            */
            Uri location;
            try
            {
                location = new Uri(line);
            }
            catch (Exception e)
            {
                throw new SitemapException("Unable to parse sitemap item", e);
            }

            yield return new UrlSetItem(location, null, null, null);
        }
    }
}
