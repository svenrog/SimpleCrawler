namespace SimpleCrawler.Js.Jint;

public sealed class JintEngineOptions
{
    /// <summary>
    /// How long a single execution call may run before Jint abandons it with a <see cref="TimeoutException"/>;
    /// <see cref="TimeSpan.Zero"/> disables the ceiling. Jint checks its constraints between statements and
    /// restarts this timer whenever the engine is re-entered, so the bound is <b>per script</b> — a page of
    /// many scripts may spend it more than once. Named for that: <c>V8EngineOptions.PageTimeout</c> is the
    /// same idea over the whole page, which is what an out-of-band interrupt can enforce and this cannot be
    /// reshaped into without reimplementing the constraint. The renderer charges it accordingly: a spent
    /// ceiling costs that one script, and a page is abandoned only once it has spent several.
    /// <para>
    /// Needed alongside the engine's stack-depth guard, which bounds recursion rather than time: a page that
    /// recurses a few frames a minute while doing exponentially more work per level holds a core indefinitely
    /// at a depth no such guard reaches.
    /// </para>
    /// </summary>
    public TimeSpan ScriptTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
