using SimpleCrawler.Core.Throttling;
using System.Diagnostics;

namespace SimpleCrawler.Core.Scheduling;

/// <summary>
/// Host-aware crawl frontier: pending URLs are partitioned into a FIFO queue per authority, and a single
/// consumer pulls the URL of whichever host is soonest allowed to fetch. Because a host's per-request
/// spacing is enforced by picking a different ready host rather than by parking the consumer, one
/// rate-limited or slow host can never hold up URLs bound for other hosts.
///
/// Sharding: no state is shared across hosts except the per-shard round-robin counter, so a partition of
/// authorities can run as an independent HostFrontier driven by its own single consumer. Keep it that way -
/// the DequeueReadyAsync signal handshake assumes exactly one consumer per instance.
/// </summary>
internal sealed class HostFrontier
{
    private readonly AdaptiveThrottler _throttling;
    private readonly Func<string, double> _baseDelay;
    private readonly Lock _lock = new();
    private readonly Dictionary<string, HostQueue> _queues;

    private long _servedSeq;
    private bool _completed;
    private TaskCompletionSource _wake;

    public HostFrontier(AdaptiveThrottler throttling, Func<string, double> baseDelay)
    {
        _throttling = throttling;
        _baseDelay = baseDelay;
        _queues = new Dictionary<string, HostQueue>(StringComparer.OrdinalIgnoreCase);
        _wake = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Buckets a URL onto its host's queue and wakes the consumer. Returns false once the frontier is
    /// completed, so the caller can balance the outstanding-count it reserved for the URL.
    /// </summary>
    public bool Enqueue(string url)
    {
        var authority = new Uri(url).Authority;

        lock (_lock)
        {
            if (_completed)
                return false;

            if (!_queues.TryGetValue(authority, out var queue))
            {
                queue = new HostQueue();
                _queues[authority] = queue;
            }

            queue.Urls.Enqueue(url);
            Wake();
            return true;
        }
    }

    /// <summary>
    /// Stops accepting new work and lets the consumer drain whatever is still queued, ignoring per-host
    /// spacing so an aborting or finishing crawl unwinds immediately.
    /// </summary>
    public void Complete()
    {
        lock (_lock)
        {
            _completed = true;
            Wake();
        }
    }

    /// <summary>
    /// Returns the next URL whose host is allowed to fetch now, waiting only when no host is ready yet.
    /// Single-consumer: exactly one caller may be in flight at a time. Returns null once the frontier is
    /// completed and drained.
    /// </summary>
    public async ValueTask<string?> DequeueReadyAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Task signal;
            long readyAt;

            lock (_lock)
            {
                if (_completed)
                    return DrainOne();

                if (_queues.Count == 0)
                {
                    signal = ResetSignal();
                    readyAt = -1;
                }
                else
                {
                    var now = Stopwatch.GetTimestamp();
                    var (host, ready) = SelectReadiest(now);

                    if (ready <= now)
                        return Dispatch(host);

                    signal = ResetSignal();
                    readyAt = ready;
                }
            }

            if (readyAt < 0)
                await signal.WaitAsync(cancellationToken);
            else
                await WaitForReadyOrSignal(signal, readyAt, cancellationToken);
        }
    }

    /// <summary>
    /// Picks the host whose next fetch slot opens soonest, breaking ties toward the least recently served
    /// host so equally-ready (e.g. unthrottled) hosts are dispatched round-robin rather than one at a time.
    /// </summary>
    private (string Host, long Ready) SelectReadiest(long now)
    {
        string? bestHost = null;
        var bestReady = 0L;
        var bestServed = 0L;

        foreach (var (authority, queue) in _queues)
        {
            var ready = _throttling.PeekNextReady(authority, _baseDelay(authority), now);
            if (bestHost is null || ready < bestReady || (ready == bestReady && queue.LastServed < bestServed))
            {
                bestHost = authority;
                bestReady = ready;
                bestServed = queue.LastServed;
            }
        }

        return (bestHost!, bestReady);
    }

    /// <summary>
    /// Reserves the host's slot and hands out one of its URLs; must be called under the lock.
    /// </summary>
    private string Dispatch(string host)
    {
        var queue = _queues[host];
        _throttling.ReserveSlot(host, _baseDelay(host));

        var url = queue.Urls.Dequeue();
        queue.LastServed = ++_servedSeq;

        if (queue.Urls.Count == 0)
            _queues.Remove(host);

        return url;
    }

    /// <summary>
    /// Hands out any queued URL without spacing, for the completed/aborting drain; must be called under the
    /// lock. Returns null once nothing is left.
    /// </summary>
    private string? DrainOne()
    {
        foreach (var (authority, queue) in _queues)
        {
            var url = queue.Urls.Dequeue();
            if (queue.Urls.Count == 0)
                _queues.Remove(authority);
            return url;
        }

        return null;
    }

    /// <summary>
    /// Returns a fresh, uncompleted wake task to await; must be called under the lock. Safe for the single
    /// consumer to reset because a producer only ever completes the current instance under the same lock.
    /// </summary>
    private Task ResetSignal()
    {
        if (_wake.Task.IsCompleted)
            _wake = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        return _wake.Task;
    }

    private void Wake()
    {
        _wake.TrySetResult();
    }

    /// <summary>
    /// Waits until the earliest-ready host's slot opens or a newly enqueued URL signals sooner, cancelling
    /// the timer on signal so a long per-host penalty never keeps a stale timer alive.
    /// </summary>
    private static async Task WaitForReadyOrSignal(Task signal, long readyAt, CancellationToken cancellationToken)
    {
        var waitTicks = readyAt - Stopwatch.GetTimestamp();
        if (waitTicks <= 0)
            return;

        var span = TimeSpan.FromSeconds((double)waitTicks / Stopwatch.Frequency);

        using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delay = Task.Delay(span, timerCts.Token);

        var winner = await Task.WhenAny(signal, delay);
        if (winner == signal)
            timerCts.Cancel();

        try
        {
            await delay;
        }
        catch (OperationCanceledException)
        {
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    /// <summary>
    /// A host's pending URLs plus the round-robin marker of when it was last dispatched.
    /// </summary>
    private sealed class HostQueue
    {
        public Queue<string> Urls { get; } = new();
        public long LastServed { get; set; }
    }
}
