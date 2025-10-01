using Crawler.Core.Models;
using System.Collections.Generic;

namespace Crawler.Benchmarks.Models;

internal class NullScrapeResult : IScrapeResult
{
    public IReadOnlyCollection<string> Urls => [];
}
