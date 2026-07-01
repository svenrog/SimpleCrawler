// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

namespace Crawler.Core.Robots;

/// <summary>
/// Change frequency values used in the sitemap specification
/// </summary>
public enum ChangeFrequency
{
    /// <summary>
    /// Describes a document that changes every time it is accessed
    /// </summary>
    Always = 0,
    /// <summary>
    /// Hints that a document is expected to change hourly
    /// </summary>
    Hourly = 1,
    /// <summary>
    /// Hints that a document is expected to change daily
    /// </summary>
    Daily = 2,
    /// <summary>
    /// Hints that a document is expected to change weekly
    /// </summary>
    Weekly = 3,
    /// <summary>
    /// Hints that a document is expected to change monthly
    /// </summary>
    Monthly = 4,
    /// <summary>
    /// Hints that a document is expected to change yearly
    /// </summary>
    Yearly = 5,
    /// <summary>
    /// Describes an archived URL
    /// </summary>
    Never = 6,
}
