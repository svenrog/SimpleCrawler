using SimpleCrawler.Core.Models;

namespace SimpleCrawler.Core;

public interface ICrawler : ICrawler<ScrapeResult> { }
public interface ICrawler<TResult>
    where TResult : IScrapeResult
{
    Task<TResult> Start(string entry, CancellationToken cancellationToken = default);
}
