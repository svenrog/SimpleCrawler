using System.Diagnostics;

namespace SimpleCrawler.Core.Throttling;

/// <summary>
/// Per-host request spacing plus an adaptive rate-limit penalty. A single lock guards all mutable state,
/// held only for the brief slot reservation or penalty update - never across the actual wait - so the
/// fetch workers space themselves out without one host's delay ever blocking another's bookkeeping.
/// </summary>
internal sealed class HostThrottle
{
    private const double _initialPenalty = 1;
    private const int _successesBeforeDecay = 10;

    private readonly Lock _lock = new();

    private long _nextSlot;
    private double _penalty;
    private int _successStreak;
    private int _active;

    public bool IsActive => Volatile.Read(ref _active) == 1;

    public double Penalty
    {
        get
        {
            lock (_lock)
                return _penalty;
        }
    }

    /// <summary>
    /// Reserves the next fetch slot, spaced delaySeconds after the previous one, and returns the Stopwatch
    /// timestamp the caller should wait until.
    /// </summary>
    public long Reserve(double delaySeconds)
    {
        var deltaTicks = delaySeconds > 0 ? (long)(delaySeconds * Stopwatch.Frequency) : 0;

        lock (_lock)
        {
            var start = Math.Max(Stopwatch.GetTimestamp(), _nextSlot);
            _nextSlot = start + deltaTicks;
            return start;
        }
    }

    /// <summary>
    /// Raises the penalty (seeded at _initialPenalty, doubling thereafter, capped at maxSeconds) and, when
    /// the response carried a Retry-After, pushes the next slot out by that grace. Returns the new penalty.
    /// </summary>
    public double PenalizeRateLimit(double maxSeconds, TimeSpan? retryAfter)
    {
        lock (_lock)
        {
            _penalty = _penalty <= 0 ? _initialPenalty : _penalty * 2;
            if (maxSeconds > 0 && _penalty > maxSeconds)
                _penalty = maxSeconds;

            _successStreak = 0;
            Volatile.Write(ref _active, 1);

            if (retryAfter is { } grace && grace > TimeSpan.Zero)
            {
                var slot = Stopwatch.GetTimestamp() + (long)(grace.TotalSeconds * Stopwatch.Frequency);
                if (slot > _nextSlot)
                    _nextSlot = slot;
            }

            return _penalty;
        }
    }

    /// <summary>
    /// Halves the penalty once a host serves _successesBeforeDecay responses in a row without rate limiting.
    /// </summary>
    public void RegisterSuccess()
    {
        if (Volatile.Read(ref _active) == 0)
            return;

        lock (_lock)
        {
            if (_penalty <= 0)
            {
                Volatile.Write(ref _active, 0);
                return;
            }

            if (++_successStreak < _successesBeforeDecay)
                return;

            _successStreak = 0;
            _penalty *= 0.5;

            if (_penalty < 0.01)
            {
                _penalty = 0;
                Volatile.Write(ref _active, 0);
            }
        }
    }
}
