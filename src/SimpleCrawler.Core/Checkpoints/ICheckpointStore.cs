namespace SimpleCrawler.Core.Checkpoints;

public interface ICheckpointStore
{
    /// <summary>
    /// Human-readable description of where the checkpoint is persisted, for log output.
    /// </summary>
    string Target { get; }

    ValueTask<CrawlState?> LoadAsync(CancellationToken cancellationToken);
    ValueTask SaveAsync(CrawlState state, CancellationToken cancellationToken);
}
