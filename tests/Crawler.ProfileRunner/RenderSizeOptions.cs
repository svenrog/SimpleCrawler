using CommandLine;

namespace Crawler.ProfileRunner;

[Verb("rendersize", HelpText = "Render the SPA once, print element/anchor counts + size, and dump the serialized HTML to disk.")]
public sealed class RenderSizeOptions
{
    [Value(0, MetaName = "combo", Default = "jint-hap", HelpText = "engine+parser: jint-js | jint-as | jint-hap | v8-js | v8-as | v8-hap")]
    public string Combo { get; set; } = "jint-hap";

    [Value(1, MetaName = "framework", Default = "preact", HelpText = "SPA framework: react | preact | vue | svelte | solid")]
    public string Framework { get; set; } = "preact";
}
