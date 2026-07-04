using BenchmarkDotNet.Attributes;
using Crawler.Core;
using Crawler.Js.AngleSharp;
using Crawler.Js.HtmlAgilityPack;
using Crawler.Js.Jint;
using Crawler.Js.V8;
using Crawler.TestHost.Infrastructure.Factories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Crawler.Benchmarks;

// Exercises the real JS render path: every route returns the same client-only SPA shell, so a crawler
// only discovers the link set after rendering it and then re-runs the engine once per discovered link.
// Each (engine x HTML parser) pair is its own benchmark so the native pre-parse backends (AngleSharp / HAP
// feeding the tree to dom.js via __crawlerLoadTree) can be compared against dom.js's own JS tokenizer.
[MemoryDiagnoser]
[ShortRunJob]
public class SpaRenderBenchmarks
{
    private const string _framework = "preact";
    private const int _port = 5290;
    private static readonly string _entry = $"http://localhost:{_port}/";

    private WebApplication _host = null!;
    private CancellationTokenSource _tokenSource = null!;
    private readonly List<ServiceProvider> _providers = new();

    private DefaultJintCrawler _jintJsParser = null!;
    private DefaultJintCrawler _jintAngleSharp = null!;
    private DefaultJintCrawler _jintHtmlAgilityPack = null!;
    private DefaultV8Crawler _v8JsParser = null!;
    private DefaultV8Crawler _v8AngleSharp = null!;
    private DefaultV8Crawler _v8HtmlAgilityPack = null!;

    [GlobalSetup]
    public void Setup()
    {
        _jintJsParser = ResolveJint(null);
        _jintAngleSharp = ResolveJint(s => s.AddAngleSharpHtmlParser());
        _jintHtmlAgilityPack = ResolveJint(s => s.AddHtmlAgilityPackHtmlParser());
        _v8JsParser = ResolveV8(null);
        _v8AngleSharp = ResolveV8(s => s.AddAngleSharpHtmlParser());
        _v8HtmlAgilityPack = ResolveV8(s => s.AddHtmlAgilityPackHtmlParser());

        _tokenSource = new CancellationTokenSource();
        _host = SpaWebApplicationFactory.Create(_entry, _framework);
        _host.StartAsync(_tokenSource.Token);
    }

    // One IHtmlParser is registered per provider so the renderer's parsers.FirstOrDefault() selects it; a null
    // parser action leaves the set empty and the renderer falls back to dom.js's __crawlerLoadHtml tokenizer.
    private DefaultJintCrawler ResolveJint(Action<IServiceCollection>? parser)
        => Build(s => s.AddJintCrawler(_options), parser).GetRequiredService<DefaultJintCrawler>();

    private DefaultV8Crawler ResolveV8(Action<IServiceCollection>? parser)
        => Build(s => s.AddV8Crawler(_options), parser).GetRequiredService<DefaultV8Crawler>();

    private ServiceProvider Build(Action<IServiceCollection> engine, Action<IServiceCollection>? parser)
    {
        var services = new ServiceCollection();
        engine(services);
        parser?.Invoke(services);
        services.AddSingleton<ILogger>(NullLogger.Instance);

        var provider = services.BuildServiceProvider();
        _providers.Add(provider);
        return provider;
    }

    private static readonly CrawlerOptions _options = new()
    {
        CrawlDelay = 0,
        Concurrency = 8,
        RespectMetaRobots = false,
        RespectRobotsTxt = false,
    };

    [Benchmark(Baseline = true)]
    public Task JintJsParser() => _jintJsParser.Start(_entry, _tokenSource.Token);

    [Benchmark]
    public Task JintAngleSharp() => _jintAngleSharp.Start(_entry, _tokenSource.Token);

    [Benchmark]
    public Task JintHtmlAgilityPack() => _jintHtmlAgilityPack.Start(_entry, _tokenSource.Token);

    [Benchmark]
    public Task V8JsParser() => _v8JsParser.Start(_entry, _tokenSource.Token);

    [Benchmark]
    public Task V8AngleSharp() => _v8AngleSharp.Start(_entry, _tokenSource.Token);

    [Benchmark]
    public Task V8HtmlAgilityPack() => _v8HtmlAgilityPack.Start(_entry, _tokenSource.Token);

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _tokenSource.CancelAsync();
        await _host.DisposeAsync();

        foreach (var provider in _providers)
        {
            await provider.DisposeAsync();
        }

        _providers.Clear();
        _tokenSource.Dispose();
    }
}
