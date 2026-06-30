namespace Crawler.Js.V8;

public sealed class V8EngineOptions
{
    /// <summary>
    /// Per-V8-isolate soft heap cap in MiB; 0 disables the cap. When an isolate's heap crosses this,
    /// ClearScript's heap monitoring interrupts the running page and throws a catchable error, so a
    /// runaway page is aborted instead of ballooning the process. The cap is per isolate, so peak memory
    /// is roughly Concurrency × this value.
    /// </summary>
    public int MaxHeapSizeMb { get; set; } = 256;
}
