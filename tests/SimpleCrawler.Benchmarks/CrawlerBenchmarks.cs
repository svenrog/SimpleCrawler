using BenchmarkDotNet.Attributes;
using SimpleCrawler.AngleSharp;
using SimpleCrawler.Core;
using SimpleCrawler.HtmlAgilityPack;
using SimpleCrawler.Js.Jint;
using SimpleCrawler.Js.V8;
using SimpleCrawler.Playwright;
using SimpleCrawler.Puppeteer;
using SimpleCrawler.TestHost.Infrastructure.Factories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SimpleCrawler.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class CrawlerBenchmarks
{
    private const string _entry = "http://localhost:5228/";

    private WebApplication _host = null!;
    private CancellationTokenSource _tokenSource = null!;
    private ServiceProvider _serviceProvider = null!;

    private DefaultHtmlAgilityPackCrawler _htmlAgilityPackCrawler = null!;
    private DefaultAngleSharpCrawler _angleSharpCrawler = null!;
    private DefaultJintCrawler _angleSharpJintCrawler = null!;
    private DefaultV8Crawler _angleSharpV8Crawler = null!;
    private DefaultPlaywrightCrawler _playwrightCrawler = null!;
    private DefaultPuppeteerCrawler _puppeteerCrawler = null!;


    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        var options = new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 8,
        };
        var headlessOptions = new HeadlessCrawlerOptions(options);

        services.AddHtmlAgilityPackCrawler(options);
        services.AddAngleSharpCrawler(options);
        services.AddJintCrawler(options);
        services.AddV8Crawler(options);
        services.AddPlaywrightCrawler(headlessOptions);
        services.AddPuppeteerCrawler(headlessOptions);
        services.AddSingleton<ILogger>(NullLogger.Instance);
        services.AddScoped<CancellationTokenSource>();

        _serviceProvider = services.BuildServiceProvider();

        _htmlAgilityPackCrawler = _serviceProvider.GetRequiredService<DefaultHtmlAgilityPackCrawler>();
        _angleSharpCrawler = _serviceProvider.GetRequiredService<DefaultAngleSharpCrawler>();
        _angleSharpJintCrawler = _serviceProvider.GetRequiredService<DefaultJintCrawler>();
        _angleSharpV8Crawler = _serviceProvider.GetRequiredService<DefaultV8Crawler>();
        _playwrightCrawler = _serviceProvider.GetRequiredService<DefaultPlaywrightCrawler>();
        _puppeteerCrawler = _serviceProvider.GetRequiredService<DefaultPuppeteerCrawler>();

        _tokenSource = _serviceProvider.GetRequiredService<CancellationTokenSource>();

        _host = StaticWebApplicationFactory.Create(_entry);
        _host.StartAsync(_tokenSource.Token);
    }

    [Benchmark]
    public async Task HtmlAgilityPackCrawl()
    {
        await _htmlAgilityPackCrawler.Start(_entry, _tokenSource.Token);
    }

    [Benchmark]
    public async Task AngleSharpCrawl()
    {
        await _angleSharpCrawler.Start(_entry, _tokenSource.Token);
    }

    [Benchmark]
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
    }
}
