using CommandLine;

namespace Crawler.ProfileRunner;

[Verb("rendersize", HelpText = "Render the SPA once, print element/anchor counts + size, and dump the serialized HTML to disk.")]
public sealed class RenderSizeOptions
{
    [Value(0, MetaName = "combo", Default = "jint", HelpText = "engine: jint | v8")]
    public string Combo { get; set; } = "jint";

    [Value(1, MetaName = "framework", Default = "preact", HelpText = "SPA framework: react | preact | vue | svelte | solid")]
    public string Framework { get; set; } = "preact";
}
