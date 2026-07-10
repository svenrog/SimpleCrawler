namespace SimpleCrawler.Core.Checkpoints;

public interface ICheckpointStore
{
    // Human-readable description of where the checkpoint is persisted, for log output.
    string Target { get; }

    ValueTask<CrawlState?> LoadAsync(CancellationToken cancellationToken);
    ValueTask SaveAsync(CrawlState state, CancellationToken cancellationToken);
}
