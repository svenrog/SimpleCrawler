namespace SimpleCrawler.Tests.Common.Extensions;

/// <summary>
/// Runs fixture teardown under a hard deadline so no single disposal can pin the test host once the runner
/// (or the IDE "Stop" button) has abandoned the run. A stalled Kestrel host or a wedged headless-browser
/// subprocess owned by a ServiceProvider is left for process exit to reclaim rather than blocking forever.
/// </summary>
internal static class TeardownGuard
{
    public static async ValueTask RunBounded(Func<Task> teardown, TimeSpan timeout)
    {
        try
        {
            await teardown().WaitAsync(timeout);
        }
        catch
        {
            // Swallowed deliberately: a timeout, cancellation, or disposal fault must never escape teardown.
            // Whatever is still stuck past the deadline is abandoned to process exit.
        }
    }
}
