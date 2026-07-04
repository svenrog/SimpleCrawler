using Crawler.Core;
using Crawler.Js.Abstractions;
using Crawler.Js.AngleSharp;
using Crawler.Js.HtmlAgilityPack;
using Crawler.Js.Jint;
using Crawler.Js.Models;
using Crawler.Js.Parsing;
using Crawler.Js.Rendering;
using Crawler.Js.V8;
using Crawler.TestHost.Infrastructure.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace Crawler.Benchmarks;

// Temporary investigation harness (not a benchmark). Drives the real crawl/render path for a single
// engine+parser combo against one framework's test-host SPA.
//
//   profile <combo> <iterations> [framework]
//     Crawls the SPA repeatedly so RenderProfiler (JSRENDER_PROFILE=1) prints a per-phase table at exit.
//   rendersize <combo> [framework]
//     Renders the SPA once, prints element/anchor counts + size, and dumps the serialized HTML to disk
//     (used to confirm every framework renders at production weight — a few thousand elements).
//
//   combo       = jint-js | jint-as | jint-hap | v8-js | v8-as | v8-hap
//   framework   = react | preact | vue | svelte | solid (default preact)
internal static class ProfileRunner
{
    private const int _port = 5299;
    private static readonly string _entry = $"http://localhost:{_port}/";

    // Mirror the internal DefaultJintCrawler/DefaultV8Crawler.EngineKey constants (internal to their
    // assemblies, so re-declared here for this out-of-assembly diagnostic harness).
    private const string _jintEngineKey = "js-jint";
    private const string _v8EngineKey = "js-v8";

    private static readonly CrawlerOptions _options = new()
    {
        CrawlDelay = 0,
        Concurrency = 8,
        RespectMetaRobots = false,
        RespectRobotsTxt = false,
        EnableSitemapDiscovery = false,
    };

    public static async Task Run(string combo, int iterations, string framework)
    {
        var tokenSource = new CancellationTokenSource();
        var host = SpaWebApplicationFactory.Create(_entry, framework);
        _ = host.StartAsync(tokenSource.Token);
        await Task.Delay(1500);

        Func<CancellationToken, Task> crawl = BuildCrawl(combo);

        await crawl(tokenSource.Token);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        var before = GC.GetTotalAllocatedBytes();
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
            await crawl(tokenSource.Token);
        sw.Stop();
        var allocated = GC.GetTotalAllocatedBytes() - before;

        Console.WriteLine();
        Console.WriteLine($"=== {combo} / {framework}: {iterations} crawls in {sw.ElapsedMilliseconds} ms " +
            $"({sw.ElapsedMilliseconds / (double)iterations:F1} ms/crawl), " +
            $"managed alloc {allocated / 1_000_000.0:F1} MB total ===");

        await tokenSource.CancelAsync();
        await host.DisposeAsync();
    }

    public static async Task RenderSize(string combo, string framework)
    {
        var tokenSource = new CancellationTokenSource();
        var host = SpaWebApplicationFactory.Create(_entry, framework);
        _ = host.StartAsync(tokenSource.Token);
        await Task.Delay(1500);

        var provider = BuildProvider(combo, out var engineKey);
        var factory = provider.GetRequiredKeyedService<IJsEngineFactory>(engineKey);
        var parser = provider.GetService<IHtmlParser>();
        var renderOptions = provider.GetRequiredService<IOptions<JsRenderOptions>>().Value;
        var renderer = new JsRenderer(factory, renderOptions, parser, NullLogger.Instance);

        using var client = new HttpClient();
        var shell = await client.GetByteArrayAsync(_entry, tokenSource.Token);
        var htmlBytes = await renderer.RenderAsync(shell, _entry, client, tokenSource.Token);
        var html = Encoding.UTF8.GetString(htmlBytes);

        var elements = Regex.Matches(html, "<[a-zA-Z][^\\s/>]*").Count;
        var anchors = Regex.Matches(html, "<a[\\s>]").Count;

        var dumpPath = Path.Combine(Path.GetTempPath(), $"rendersize-{framework}-{combo}.html");
        await File.WriteAllTextAsync(dumpPath, html, tokenSource.Token);

        Console.WriteLine();
        Console.WriteLine($"=== rendersize {combo} / {framework}: {elements} elements, {anchors} anchors, " +
            $"{html.Length / 1024.0:F0} KB ===");
        Console.WriteLine($"HTML dumped to {dumpPath}");

        await tokenSource.CancelAsync();
        await host.DisposeAsync();
    }

    private static Func<CancellationToken, Task> BuildCrawl(string combo)
    {
        var provider = BuildProvider(combo, out _);

        if (combo.StartsWith("jint"))
        {
            var crawler = provider.GetRequiredService<DefaultJintCrawler>();
            return ct => crawler.Start(_entry, ct);
        }
        else
        {
            var crawler = provider.GetRequiredService<DefaultV8Crawler>();
            return ct => crawler.Start(_entry, ct);
        }
    }

    private static ServiceProvider BuildProvider(string combo, out string engineKey)
    {
        Action<IServiceCollection> parser = combo switch
        {
            "jint-as" or "v8-as" => s => s.AddAngleSharpHtmlParser(),
            "jint-hap" or "v8-hap" => s => s.AddHtmlAgilityPackHtmlParser(),
            _ => null,
        };

        if (combo.StartsWith("jint"))
        {
            engineKey = _jintEngineKey;
            return Build(s => s.AddJintCrawler(_options, new JsRenderOptions()), parser);
        }
        else
        {
            engineKey = _v8EngineKey;
            return Build(s => s.AddV8Crawler(_options), parser);
        }
    }

    private static ServiceProvider Build(Action<IServiceCollection> engine, Action<IServiceCollection> parser)
    {
        var services = new ServiceCollection();
        engine(services);
        parser?.Invoke(services);
        services.AddSingleton<ILogger>(NullLogger.Instance);
        return services.BuildServiceProvider();
    }
}
