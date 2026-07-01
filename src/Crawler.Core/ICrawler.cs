using Crawler.Core.Models;

namespace Crawler.Core;

public interface ICrawler : ICrawler<ScrapeResult> { }
public interface ICrawler<TResult>
    where TResult : IScrapeResult
{
    Task<TResult> Start(string entry, CancellationToken cancellationToken = default);
}
