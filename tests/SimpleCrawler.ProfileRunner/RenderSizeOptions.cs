using CommandLine;

namespace SimpleCrawler.ProfileRunner;

[Verb("rendersize", HelpText = "Render the SPA once, print element/anchor counts + size, and dump the serialized HTML to disk.")]
public sealed class RenderSizeOptions
{
    [Value(0, MetaName = "combo", Default = "jint", HelpText = "engine: jint | v8")]
    public string Combo { get; set; } = "jint";

    [Value(1, MetaName = "framework", Default = "preact", HelpText = "SPA framework: react | preact | vue | svelte | solid")]
    public string Framework { get; set; } = "preact";

    [Option("url", HelpText = "Render an arbitrary live URL instead of the test-host SPA.")]
    public string? Url { get; set; }

    [Option("fetch", HelpText = "Enable the fetch/XHR shim (JsRenderOptions.EnableFetch).")]
    public bool EnableFetch { get; set; }

    [Option("streams", HelpText = "Enable the WHATWG Streams shim (JsRenderOptions.EnableStreams).")]
    public bool EnableStreams { get; set; }
}
