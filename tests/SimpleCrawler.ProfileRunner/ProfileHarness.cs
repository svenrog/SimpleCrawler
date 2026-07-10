using SimpleCrawler.Core;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Jint;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.Rendering;
using SimpleCrawler.Js.V8;
using SimpleCrawler.TestHost.Infrastructure.Factories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;

namespace SimpleCrawler.ProfileRunner;

/// <summary>
/// Investigation harness (not a benchmark). Drives the real crawl/render path for a single
/// engine+parser combo against one framework's test-host SPA.
/// </summary>
internal static partial class ProfileHarness
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

    public static async Task RenderSize(string combo, string framework, string? url = null, bool enableFetch = false, bool enableStreams = false)
    {
        var tokenSource = new CancellationTokenSource();
        IAsyncDisposable? host = null;
        var entry = _entry;
        if (url is null)
        {
            var spaHost = SpaWebApplicationFactory.Create(_entry, framework);
            host = spaHost;
            _ = spaHost.StartAsync(tokenSource.Token);
            await Task.Delay(1500);
        }
        else
        {
            entry = url;
        }

        var provider = BuildProvider(combo, out var engineKey);
        var factory = provider.GetRequiredKeyedService<IJsEngineFactory>(engineKey);
        var renderOptions = url is null
            ? provider.GetRequiredService<IOptions<JsRenderOptions>>().Value
            : new JsRenderOptions { EnableFetch = enableFetch, EnableStreams = enableStreams, ScriptLogging = LogLevel.Warning };
        ILogger logger = url is null ? NullLogger.Instance : new ConsoleLogger();
        var renderer = new JsRenderer(factory, renderOptions, logger);

        using var client = new HttpClient();
        var shell = await client.GetByteArrayAsync(entry, tokenSource.Token);
        var htmlBytes = await renderer.RenderAsync(shell, entry, client, tokenSource.Token);
        var html = Encoding.UTF8.GetString(htmlBytes);

        var elements = Elements().Count(html);
        var anchors = Anchors().Count(html);

        var label = url is null ? framework : "live";
        var flags = url is null ? "" : $" (fetch={enableFetch}, streams={enableStreams})";
        var suffix = url is null ? "" : $"-streams{enableStreams}";
        var dumpPath = Path.Combine(Path.GetTempPath(), $"rendersize-{label}-{combo}{suffix}.html");
        await File.WriteAllTextAsync(dumpPath, html, tokenSource.Token);

        Console.WriteLine();
        Console.WriteLine($"=== rendersize {combo} / {label}{flags}: {elements} elements, {anchors} anchors, " +
            $"{html.Length / 1024.0:F0} KB ===");
        Console.WriteLine($"HTML dumped to {dumpPath}");

        await tokenSource.CancelAsync();
        if (host is not null)
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
        if (combo.StartsWith("jint"))
        {
            engineKey = _jintEngineKey;
            return Build(s => s.AddJintCrawler(_options, new JsRenderOptions()));
        }
        else
        {
            engineKey = _v8EngineKey;
            return Build(s => s.AddV8Crawler(_options));
        }
    }

    private static ServiceProvider Build(Action<IServiceCollection> engine)
    {
        var services = new ServiceCollection();
        engine(services);
        services.AddSingleton<ILogger>(NullLogger.Instance);
        return services.BuildServiceProvider();
    }

    private sealed class ConsoleLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Console.WriteLine($"[{logLevel}] {formatter(state, exception)}");
    }

    [GeneratedRegex("<a[\\s>]")]
    private static partial Regex Anchors();
    [GeneratedRegex("<[a-zA-Z][^\\s/>]*")]
    private static partial Regex Elements();
}
