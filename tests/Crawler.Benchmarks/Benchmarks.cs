using BenchmarkDotNet.Attributes;
using Crawler.AngleSharp;
using Crawler.Core;
using Crawler.HtmlAgilityPack;
using Crawler.Playwright;
using Crawler.TestHost.Infrastructure.Factories;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Crawler.Benchmarks;

[MemoryDiagnoser]
[ShortRunJob]
public class Benchmarks
{
    private const string _entry = "http://localhost:5228/";

    private WebApplication _host;
    private CancellationTokenSource _tokenSource;
    private ServiceProvider _serviceProvider;

    private DefaultHtmlAgilityPackCrawler _htmlAgilityPackCrawler;
    private DefaultAngleSharpCrawler _angleSharpCrawler;
    private DefaultPlaywrightCrawler _playwrightCrawler;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        var options = new CrawlerOptions
        {
            CrawlDelay = 0,
            Parallelism = 8,
        };

        services.AddHtmlAgilityPackCrawler(options);
        services.AddAngleSharpCrawler(options);
        services.AddPlaywrightCrawler(options);
        services.AddSingleton<ILogger>(NullLogger.Instance);
        services.AddScoped<CancellationTokenSource>();

        _serviceProvider = services.BuildServiceProvider();

        _htmlAgilityPackCrawler = _serviceProvider.GetRequiredService<DefaultHtmlAgilityPackCrawler>();
        _angleSharpCrawler = _serviceProvider.GetRequiredService<DefaultAngleSharpCrawler>();
        _playwrightCrawler = _serviceProvider.GetRequiredService<DefaultPlaywrightCrawler>();

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
    public async Task PlaywrightCrawl()
    {
        await _playwrightCrawler.Start(_entry, _tokenSource.Token);
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
