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

    public void LogEnabled()
    {
        _logger.LogInformation("Checkpointing to '{target}' every {interval:0.#}s.", _store.Target, _interval.TotalSeconds);
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
            _logger.LogWarning("Failed to read checkpoint from '{target}'; starting a fresh crawl: {message}", _store.Target, e.Message);
            return null;
        }

        if (state is null || !EntriesMatch(state.Entries, entries))
            return null;

        state.RebuildAfterLoad();
        return state;
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
            _logger.LogDebug("Wrote checkpoint to '{target}' ({processed} processed, {pending} pending).",
                _store.Target, state.Processed.Count, state.Frontier.Count);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogWarning("Failed to write checkpoint to '{target}': {message}", _store.Target, e.Message);
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
