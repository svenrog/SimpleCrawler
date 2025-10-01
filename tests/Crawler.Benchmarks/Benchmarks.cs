using AngleSharp;
using BenchmarkDotNet.Attributes;
using Crawler.Benchmarks.Crawlers;
using Crawler.Core;
using Crawler.TestHost.Infrastructure.Extensions;
using Crawler.TestHost.Infrastructure.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System;
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
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", _entry);

        var services = new ServiceCollection();
        var options = new CrawlerOptions
        {
            Entry = _entry,
            CrawlDelay = 0,
            Parallelism = 4,
        };

        services.AddSingleton<ILogger>(NullLogger.Instance);
        services.AddSingleton(Options.Create(options));
        services.AddSingleton<HttpClient>();
        services.AddSingleton(Configuration.Default.WithDefaultLoader());

        _serviceProvider = services.BuildServiceProvider();

        var builder = WebApplication.CreateSlimBuilder();
        var defaultHtml = ResourceHelper.GetResponse("default")
            ?? throw new InvalidOperationException("Default response cannot be null here");

        _host = builder.Build();
        _tokenSource = new CancellationTokenSource();

        _host.MapGet("/{*path}", () => Results.Extensions.Html(defaultHtml));
        _host.RunAsync(_tokenSource.Token);
    }

    [IterationSetup(Target = nameof(HtmlAgilityPackCrawl))]
    public void HtmlAgilityPackCrawlSetup()
    {
        _htmlAgilityPackCrawler = ActivatorUtilities.CreateInstance<HtmlAgilityPackCrawler>(_serviceProvider);
    }

    [Benchmark]
    public async Task HtmlAgilityPackCrawl()
    {
        await _htmlAgilityPackCrawler.Scrape(_tokenSource.Token);
    }

    [IterationSetup(Target = nameof(AngleSharpCrawl))]
    public void AngleSharpCrawlSetup()
    {
        _angleSharpCrawler = ActivatorUtilities.CreateInstance<AngleSharpCrawler>(_serviceProvider);
    }

    [Benchmark]
    public async Task AngleSharpCrawl()
    {
        await _angleSharpCrawler.Scrape(_tokenSource.Token);
    }

    [IterationSetup(Target = nameof(PlaywrightCrawl))]
    public void PlaywrightCrawlSetup()
    {
        _playwrightCrawler = ActivatorUtilities.CreateInstance<PlaywrightCrawler>(_serviceProvider);
    }

    [Benchmark]
    public async Task PlaywrightCrawl()
    {
        await _playwrightCrawler.Scrape(_tokenSource.Token);
    }

    [IterationCleanup(Target = nameof(PlaywrightCrawl))]
    public void PlaywrightCrawlCleanup()
    {
        var task = _playwrightCrawler.DisposeAsync();
        if (task.IsCompleted)
            return;

        task.AsTask().GetAwaiter().GetResult();
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
