// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

namespace Crawler.Core.Robots;

/// <summary>
/// Url item described in a sitemap
/// </summary>
public record UrlSetItem : SitemapItem
{
    internal UrlSetItem(Uri location, DateTime? lastModified, ChangeFrequency? changeFrequency, decimal? priority)
        : base(location, lastModified)
    {
        ChangeFrequency = changeFrequency;
        Priority = priority;
    }

    /// <summary>
    /// Hint for how often the URL is expected to change
    /// </summary>
    public ChangeFrequency? ChangeFrequency { get; }

    /// <summary>
    /// Hint for the priority that should be assigned to the URL
    /// </summary>
    public decimal? Priority { get; }
}
