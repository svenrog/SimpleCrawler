// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

namespace SimpleCrawler.Core.Robots;

public record SitemapItem
{
    internal SitemapItem(Uri Location, DateTime? LastModified)
    {
        this.Location = Location;
        this.LastModified = LastModified;
    }

    /// <summary>
    /// URL location
    /// </summary>
    public Uri Location { get; }

    /// <summary>
    /// Date and time that the contents of the URL was last modified
    /// </summary>
    public DateTime? LastModified { get; }
}