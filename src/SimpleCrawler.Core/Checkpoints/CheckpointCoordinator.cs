using Microsoft.Extensions.Logging;

namespace SimpleCrawler.Core.Checkpoints;

internal sealed class CheckpointCoordinator
{
    private readonly ICheckpointStore _store;
    private readonly TimeSpan _interval;
    private readonly ILogger _logger;

    public CheckpointCoordinator(ICheckpointStore store, TimeSpan interval, ILogger logger)
    {
        _store = store;
        _interval = interval;
        _logger = logger;
    }

    public async ValueTask<CrawlState?> LoadAsync(IReadOnlyList<string> entries, CancellationToken cancellationToken)
    {
        CrawlState? state;

        try
        {
            state = await _store.LoadAsync(cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning("Failed to read checkpoint; starting a fresh crawl: {message}", e.Message);
            return null;
        }

        return state is not null && EntriesMatch(state.Entries, entries) ? state : null;
    }

    public async Task RunAutosaveAsync(Func<CrawlState> capture, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(_interval, cancellationToken);
                await SaveAsync(capture(), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask SaveAsync(CrawlState state, CancellationToken cancellationToken)
    {
        try
        {
            await _store.SaveAsync(state, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning("Failed to write checkpoint: {message}", e.Message);
        }
    }

    private static bool EntriesMatch(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count != b.Count)
            return false;

        var set = new HashSet<string>(a, StringComparer.Ordinal);
        return b.All(set.Contains);
    }
}
