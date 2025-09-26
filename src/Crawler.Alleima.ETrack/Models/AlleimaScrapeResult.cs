using Crawler.Core.Models;

namespace Crawler.Alleima.ETrack.Models;

public class AlleimaScrapeResult : IScrapeResult
{
    public required IReadOnlyCollection<string> Variations { get; set; }
    public required IReadOnlyCollection<string> Products { get; set; }
    public required IReadOnlyCollection<string> Categories { get; set; }
    public required IReadOnlyCollection<string> Other { get; set; }

    public IReadOnlyCollection<string> Urls => [.. Categories, .. Products, .. Variations, .. Other];
}
