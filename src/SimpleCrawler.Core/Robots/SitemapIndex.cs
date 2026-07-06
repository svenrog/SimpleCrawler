// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

namespace SimpleCrawler.Core.Robots;

internal class SitemapIndex : Sitemap
{
    public SitemapIndex(IAsyncEnumerable<Uri> sitemapUris) : base(Empty<UrlSetItem>())
    {
        SitemapUris = sitemapUris;
    }

    public IAsyncEnumerable<Uri> SitemapUris { get; }

#pragma warning disable CS1998
    private static async IAsyncEnumerable<T> Empty<T>()
#pragma warning restore CS1998
    {
        yield break;
    }
}
