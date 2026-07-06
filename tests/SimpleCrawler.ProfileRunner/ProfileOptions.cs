using CommandLine;

namespace SimpleCrawler.ProfileRunner;

[Verb("profile", HelpText = "Crawl the SPA repeatedly so RenderProfiler (JSRENDER_PROFILE=1) prints a per-phase table at exit.")]
public sealed class ProfileOptions
{
    [Value(0, MetaName = "combo", Default = "jint", HelpText = "engine: jint | v8")]
    public string Combo { get; set; } = "jint";

    [Value(1, MetaName = "iterations", Default = 5, HelpText = "How many times to crawl the SPA.")]
    public int Iterations { get; set; } = 5;

    [Value(2, MetaName = "framework", Default = "preact", HelpText = "SPA framework: react | preact | vue | svelte | solid")]
    public string Framework { get; set; } = "preact";
}
