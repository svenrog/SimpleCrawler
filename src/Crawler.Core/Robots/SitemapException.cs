// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;

namespace Crawler.Core.Robots;

/// <summary>
/// Exception raised when parsing a sitemap
/// </summary>
[Serializable]
public class SitemapException : Exception
{
    internal SitemapException()
    {
    }

    internal SitemapException(string? message) : base(message)
    {
    }

    internal SitemapException(string? message, Exception? innerException) : base(message, innerException)
    {
    }

    [ExcludeFromCodeCoverage]
    protected SitemapException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }
}
