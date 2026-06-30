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
// Only crawlers that execute JS are included — a static crawler sees the empty shell and follows nothing,
// so it would crawl a single page and isn't comparable. Headless (Playwright/Puppeteer) is the ceiling.
[MemoryDiagnoser]
[ShortRunJob]
public class SpaRenderBenchmarks
{
    private const string _framework = "preact";
    private const int _port = 5290;
    private static readonly string _entry = $"http://localhost:{_port}/";

    private WebApplication _host;
    private CancellationTokenSource _tokenSource;
    private ServiceProvider _serviceProvider;

    private DefaultJintCrawler _angleSharpJintCrawler;
    private DefaultV8Crawler _angleSharpV8Crawler;
    private DefaultPlaywrightCrawler _playwrightCrawler;
    private DefaultPuppeteerCrawler _puppeteerCrawler;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        var options = new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 8,
            RespectMetaRobots = false,
            RespectRobotsTxt = false,
        };

        services.AddJintCrawler(options);
        services.AddV8Crawler(options);
        services.AddPlaywrightCrawler(options);
        services.AddPuppeteerCrawler(options);
        services.AddSingleton<ILogger>(NullLogger.Instance);
        services.AddScoped<CancellationTokenSource>();

        _serviceProvider = services.BuildServiceProvider();

        _angleSharpJintCrawler = _serviceProvider.GetRequiredService<DefaultJintCrawler>();
        _angleSharpV8Crawler = _serviceProvider.GetRequiredService<DefaultV8Crawler>();
        _playwrightCrawler = _serviceProvider.GetRequiredService<DefaultPlaywrightCrawler>();
        _puppeteerCrawler = _serviceProvider.GetService<DefaultPuppeteerCrawler>();

        _tokenSource = _serviceProvider.GetRequiredService<CancellationTokenSource>();

        _host = SpaWebApplicationFactory.Create(_entry, _framework);
        _host.StartAsync(_tokenSource.Token);
    }

    [Benchmark(Baseline = true)]
    public async Task AngleSharpJintCrawl()
    {
        await _angleSharpJintCrawler.Start(_entry, _tokenSource.Token);
    }

    [Benchmark]
    public async Task AngleSharpV8Crawl()
    {
        await _angleSharpV8Crawler.Start(_entry, _tokenSource.Token);
    }

    [Benchmark]
    public async Task PlaywrightCrawl()
    {
        await _playwrightCrawler.Start(_entry, _tokenSource.Token);
    }

    [Benchmark]
    public async Task PuppeteerCrawl()
    {
        await _puppeteerCrawler.Start(_entry, _tokenSource.Token);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _tokenSource.CancelAsync();
        await _host.DisposeAsync();

        await _serviceProvider.DisposeAsync();
        _serviceProvider = null;
        _tokenSource = null;
    }
}
