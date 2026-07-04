using CommandLine;

namespace Crawler.ProfileRunner;

[Verb("profile", HelpText = "Crawl the SPA repeatedly so RenderProfiler (JSRENDER_PROFILE=1) prints a per-phase table at exit.")]
public sealed class ProfileOptions
{
    [Value(0, MetaName = "combo", Default = "jint-hap", HelpText = "engine+parser: jint-js | jint-as | jint-hap | v8-js | v8-as | v8-hap")]
    public string Combo { get; set; } = "jint-hap";

    [Value(1, MetaName = "iterations", Default = 20, HelpText = "How many times to crawl the SPA.")]
    public int Iterations { get; set; } = 20;

    [Value(2, MetaName = "framework", Default = "preact", HelpText = "SPA framework: react | preact | vue | svelte | solid")]
    public string Framework { get; set; } = "preact";
}
