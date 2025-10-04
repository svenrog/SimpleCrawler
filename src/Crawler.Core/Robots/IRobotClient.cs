// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

namespace Crawler.Core.Robots;

/// <summary>
/// Client for retrieving robots.txt
/// </summary>
public interface IRobotClient
{
    /// <summary>
    /// Loads and parses the <see cref="IRobotsTxt"/> file from the website
    /// </summary>
    /// <exception cref="HttpRequestException">Thrown if a status code that cannot be handled is returned.</exception>
    Task<IRobotsTxt> LoadRobotsTxtAsync(Uri url, CancellationToken cancellationToken = default);

    protected internal IAsyncEnumerable<UrlSetItem> LoadSitemapsAsync(Uri uri, DateTime? modifiedSince = null, CancellationToken cancellationToken = default);
}
