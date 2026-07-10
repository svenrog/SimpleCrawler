namespace SimpleCrawler.Core.Scheduling;

/// <summary>
/// An optional background operation (checkpoint autosave, progress reporting) that runs alongside the
/// crawl workers under its own cancellation, stopped in reverse-start order at the end of Start. An
/// idle sidecar (<see cref="None"/>) represents a disabled feature and is a no-op to stop and dispose.
/// </summary>
internal sealed class BackgroundOperation : IDisposable
{
    private readonly CancellationTokenSource? _cts;
    private readonly Task? _task;

    private BackgroundOperation(CancellationTokenSource? cts, Task? task)
    {
        _cts = cts;
        _task = task;
    }

    public static BackgroundOperation None() => new(null, null);

    public static BackgroundOperation Start(CancellationTokenSource cts, Task task) => new(cts, task);

    /// <summary>
    /// Signals cancellation and awaits the operation's exit; a no-op for an idle sidecar.
    /// </summary>
    public async ValueTask StopAsync()
    {
        if (_task is null)
            return;

        _cts?.Cancel();
        await _task;
    }

    public void Dispose() => _cts?.Dispose();
}
