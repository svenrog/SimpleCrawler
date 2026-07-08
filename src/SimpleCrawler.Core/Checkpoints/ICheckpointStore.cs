namespace SimpleCrawler.Core.Checkpoints;

public interface ICheckpointStore
{
    ValueTask<CrawlState?> LoadAsync(CancellationToken cancellationToken);
    ValueTask SaveAsync(CrawlState state, CancellationToken cancellationToken);
}
