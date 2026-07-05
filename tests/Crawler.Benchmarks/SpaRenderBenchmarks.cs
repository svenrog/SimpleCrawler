using BenchmarkDotNet.Attributes;
using Crawler.Core;
using Crawler.Js.Jint;
using Crawler.Js.V8;
using Crawler.Playwright;
using Crawler.Puppeteer;
using Crawler.TestHost.Infrastructure.Factories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Crawler.Benchmarks;

// Exercises the real JS render path: every route returns the same client-only SPA shell, so a crawler
// only discovers the link set after rendering it and then re-runs the engine once per discovered link.
// The in-process engines (Jint, V8) render via dom.js's own tokenizer; Playwright and Puppeteer render
// the same site in a real headless browser as an upper reference point for what they are competing against.
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

    private DefaultJintCrawler _jint = null!;
    private DefaultV8Crawler _v8 = null!;
    private DefaultPlaywrightCrawler _playwright = null!;
    private DefaultPuppeteerCrawler _puppeteer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _jint = Build(s => s.AddJintCrawler(_options)).GetRequiredService<DefaultJintCrawler>();
        _v8 = Build(s => s.AddV8Crawler(_options)).GetRequiredService<DefaultV8Crawler>();
        _playwright = Build(s => s.AddPlaywrightCrawler(_headlessOptions)).GetRequiredService<DefaultPlaywrightCrawler>();
        _puppeteer = Build(s => s.AddPuppeteerCrawler(_headlessOptions)).GetRequiredService<DefaultPuppeteerCrawler>();

        _tokenSource = new CancellationTokenSource();
        _host = SpaWebApplicationFactory.Create(_entry, _framework);
        _host.StartAsync(_tokenSource.Token);
    }

    private ServiceProvider Build(Action<IServiceCollection> engine)
    {
        var services = new ServiceCollection();
        engine(services);
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

    private static readonly HeadlessCrawlerOptions _headlessOptions = new(_options)
    {
        BlockNonEssentialResources = true,
        NetworkIdleGraceMs = 500,
    };

    [Benchmark(Baseline = true)]
    public Task Jint() => _jint.Start(_entry, _tokenSource.Token);

    [Benchmark]
    public Task V8() => _v8.Start(_entry, _tokenSource.Token);

    [Benchmark]
    public Task Playwright() => _playwright.Start(_entry, _tokenSource.Token);

    [Benchmark]
    public Task Puppeteer() => _puppeteer.Start(_entry, _tokenSource.Token);

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
