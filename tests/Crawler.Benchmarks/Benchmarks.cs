using AngleSharp;
using BenchmarkDotNet.Attributes;
using Crawler.Core;
using Crawler.TestHost.Infrastructure.Factories;
using Crawler.Tests.Common.Crawlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            Entry = _entry,
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

        _tokenSource = new CancellationTokenSource();

        _host = StaticWebApplicationFactory.Create(_entry);
        _host.RunAsync(_tokenSource.Token);
    }

    [IterationSetup(Target = nameof(HtmlAgilityPackCrawl))]
    public void HtmlAgilityPackCrawlSetup()
    {
        _htmlAgilityPackCrawler = _serviceProvider.GetRequiredService<HtmlAgilityPackCrawler>();
    }

    [Benchmark]
    public async Task HtmlAgilityPackCrawl()
    {
        await _htmlAgilityPackCrawler.Scrape(_tokenSource.Token);
    }

    [IterationSetup(Target = nameof(AngleSharpCrawl))]
    public void AngleSharpCrawlSetup()
    {
        _angleSharpCrawler = _serviceProvider.GetRequiredService<AngleSharpCrawler>();
    }

    [Benchmark]
    public async Task AngleSharpCrawl()
    {
        await _angleSharpCrawler.Scrape(_tokenSource.Token);
    }

    [IterationSetup(Target = nameof(PlaywrightCrawl))]
    public void PlaywrightCrawlSetup()
    {
        _playwrightCrawler = _serviceProvider.GetRequiredService<PlaywrightCrawler>();
    }

    [Benchmark]
    public async Task PlaywrightCrawl()
    {
        await _playwrightCrawler.Scrape(_tokenSource.Token);
    }

    [IterationCleanup(Target = nameof(PlaywrightCrawl))]
    public void PlaywrightCrawlCleanup()
    {
        var valueTask = _playwrightCrawler.DisposeAsync();
        if (valueTask.IsCompleted)
            return;

        valueTask.AsTask().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _tokenSource.CancelAsync();
        await _host.DisposeAsync();

        _tokenSource.Dispose();
        _tokenSource = null;

        _serviceProvider.Dispose();
        _serviceProvider = null;
    }

}
