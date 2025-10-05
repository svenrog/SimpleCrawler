using Crawler.Core.Models;

namespace Crawler.Tests.Common.Models;

internal class ScrapeResult : IScrapeResult
{
    public IReadOnlyCollection<string> Urls { get; set; } = [];
}
