using Crawler.Core;
using Crawler.Js.AngleSharp;
using Crawler.Js.HtmlAgilityPack;
using Crawler.Js.Jint;
using Crawler.Js.Models;
using Crawler.Js.V8;
using Crawler.TestHost.Infrastructure.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;

namespace Crawler.Benchmarks;

// Temporary investigation harness (not a benchmark). Drives the real crawl/render path for a single
// engine+parser combo so RenderProfiler (JSRENDER_PROFILE=1) prints a clean per-phase table at exit.
// Usage: dotnet run -c Release --project tests/Crawler.Benchmarks -- profile <combo> <iterations> [jintMaxUses]
//   combo       = jint-js | jint-as | jint-hap | v8-js | v8-as | v8-hap
//   jintMaxUses = optional Jint engine-pool cap for A/B runs (0 = fresh engine per page); omitted = default
internal static class ProfileRunner
{
    private const int _port = 5299;
    private static readonly string _entry = $"http://localhost:{_port}/";

    private static readonly CrawlerOptions _options = new()
    {
        CrawlDelay = 0,
        Concurrency = 8,
        RespectMetaRobots = false,
        RespectRobotsTxt = false,
    };

    public static async Task Run(string combo, int iterations, int? jintMaxUses = null)
    {
        var tokenSource = new CancellationTokenSource();
        var host = SpaWebApplicationFactory.Create(_entry, "preact");
        _ = host.StartAsync(tokenSource.Token);
        await Task.Delay(1500);

        Func<CancellationToken, Task> crawl = BuildCrawl(combo, jintMaxUses);

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
        Console.WriteLine($"=== {combo}: {iterations} crawls in {sw.ElapsedMilliseconds} ms " +
            $"({sw.ElapsedMilliseconds / (double)iterations:F1} ms/crawl), " +
            $"managed alloc {allocated / 1_000_000.0:F1} MB total ===");

        await tokenSource.CancelAsync();
        await host.DisposeAsync();
    }

    private static Func<CancellationToken, Task> BuildCrawl(string combo, int? jintMaxUses)
    {
        Action<IServiceCollection> parser = combo switch
        {
            "jint-as" or "v8-as" => s => s.AddAngleSharpHtmlParser(),
            "jint-hap" or "v8-hap" => s => s.AddHtmlAgilityPackHtmlParser(),
            _ => null,
        };

        if (combo.StartsWith("jint"))
        {
            // jintMaxUses toggles the engine pool for A/B measurement: 0 = fresh engine per page (un-pooled
            // baseline), >0 = reuse each engine for that many pages. Omitted keeps the production default.
            var engineOptions = jintMaxUses is { } maxUses ? new JintEngineOptions { MaxUsesPerEngine = maxUses } : new JintEngineOptions();

            var crawler = Build(s => s.AddJintCrawler(_options, new JsRenderOptions(), engineOptions), parser).GetRequiredService<DefaultJintCrawler>();
            return ct => crawler.Start(_entry, ct);
        }
        else
        {
            var crawler = Build(s => s.AddV8Crawler(_options), parser).GetRequiredService<DefaultV8Crawler>();
            return ct => crawler.Start(_entry, ct);
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
