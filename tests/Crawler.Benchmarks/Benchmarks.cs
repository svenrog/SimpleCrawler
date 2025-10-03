using AngleSharp;
using BenchmarkDotNet.Attributes;
using Crawler.Core;
using Crawler.TestHost.Infrastructure.Factories;
using Crawler.Tests.Common.Crawlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net.Http;
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

    private HtmlAgilityPackCrawler _htmlAgilityPackCrawler;
    private AngleSharpCrawler _angleSharpCrawler;
    private PlaywrightCrawler _playwrightCrawler;

    [GlobalSetup]
    public void Setup()
    {
        var services = new ServiceCollection();
        var options = new CrawlerOptions
        {
            CrawlDelay = 0,
            Parallelism = 4,
        };

        services.AddSingleton(Options.Create(options));
        services.AddSingleton<ILogger>(NullLogger.Instance);
        services.AddSingleton<HttpClient>();
        services.AddSingleton(Configuration.Default.WithDefaultLoader());
        services.AddScoped<HtmlAgilityPackCrawler>();
        services.AddScoped<AngleSharpCrawler>();
        services.AddScoped<PlaywrightCrawler>();

        _serviceProvider = services.BuildServiceProvider();

        _htmlAgilityPackCrawler = _serviceProvider.GetRequiredService<HtmlAgilityPackCrawler>();
        _angleSharpCrawler = _serviceProvider.GetRequiredService<AngleSharpCrawler>();
        _playwrightCrawler = _serviceProvider.GetRequiredService<PlaywrightCrawler>();

        _tokenSource = new CancellationTokenSource();

        _host = StaticWebApplicationFactory.Create(_entry);
        _host.StartAsync(_tokenSource.Token);
    }

    [Benchmark]
    public async Task HtmlAgilityPackCrawl()
    {
        await _htmlAgilityPackCrawler.Scrape(_entry, _tokenSource.Token);
    }

    [Benchmark]
    public async Task AngleSharpCrawl()
    {
        await _angleSharpCrawler.Scrape(_entry, _tokenSource.Token);
    }

    [Benchmark]
    public async Task PlaywrightCrawl()
    {
        await _playwrightCrawler.Scrape(_entry, _tokenSource.Token);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _playwrightCrawler.DisposeAsync();

        await _tokenSource.CancelAsync();
        await _host.DisposeAsync();

        _tokenSource.Dispose();
        _tokenSource = null;

        _serviceProvider.Dispose();
        _serviceProvider = null;
    }

}
