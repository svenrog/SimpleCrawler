namespace SimpleCrawler.Js.V8;

public sealed class V8EngineOptions
{
    /// <summary>
    /// Per-V8-isolate soft heap cap in MiB; 0 disables the cap. When an isolate's heap crosses this,
    /// ClearScript's heap monitoring interrupts the running page and throws a catchable error, so a
    /// runaway page is aborted instead of ballooning the process. The cap is per isolate, so peak memory
    /// is roughly Concurrency × this value.
    /// </summary>
    public int MaxHeapSizeMb { get; set; } = 256;

    /// <summary>
    /// Per-V8-isolate cap in number of uses; 0 disables the cap. When an isolate has been used 
    /// this many times it gets disposed and a new isolate is created.
    /// </summary>
    public int MaxUsesPerRuntime { get; set; } = 50;

    /// <summary>
    /// The sample rate at which ClearScript checks the V8 heap size. Lower values increase accuracy but also CPU usage.
    /// </summary>
    public TimeSpan HeapSampleInterval { get; set; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// How long one page may run scripts before it is interrupted and the render fails with a
    /// <see cref="TimeoutException"/>; <see cref="TimeSpan.Zero"/> disables the ceiling. V8 exposes no
    /// per-statement constraint hook, so this is enforced from another thread and the timer runs from the
    /// engine's creation — the bound is <b>per page</b>, and every script the page runs draws on the one
    /// ceiling. Named for that: <c>JintEngineOptions.ScriptTimeout</c> is the same idea per execution call,
    /// which is what a between-statements check can enforce and this cannot be reshaped into without arming
    /// a timer around every crossing.
    /// <para>
    /// Distinct from <see cref="MaxHeapSizeMb"/>, which interrupts a page for what it allocates: a runaway
    /// that allocates nothing is bounded by this and by nothing else.
    /// </para>
    /// </summary>
    public TimeSpan PageTimeout { get; set; } = TimeSpan.FromSeconds(30);
}
