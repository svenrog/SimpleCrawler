// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

namespace SimpleCrawler.Core.Robots;

/// <summary>
/// Describes a Sitemap
/// </summary>
public class Sitemap : ISitemap
{
    public Sitemap(IAsyncEnumerable<UrlSetItem> urlSet)
    {
        UrlSet = urlSet;
    }

    /// <inheritdoc />
    public IAsyncEnumerable<UrlSetItem> UrlSet { get; }
}
