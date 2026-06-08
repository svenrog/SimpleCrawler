using BenchmarkDotNet.Attributes;
using Crawler.Core;
using Crawler.HtmlAgilityPack;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace Crawler.Benchmarks;

// Compares coupled fetch/parse concurrency against a decoupled split, over a server with real
// per-request latency and a parse-heavy payload. Shows that splitting (high fetch, bounded parse)
// beats any coupled setting once concurrent parsing (HtmlDocument.Load) saturates the CPU.
[MemoryDiagnoser]
[ShortRunJob]
public class PipelineBenchmarks
{
    private const int _port = 5231;
    private const int _latencyMs = 25;
    private const int _pageCount = 500;
    private const int _fanout = 8;

    private WebApplication _host;
    private ServiceProvider _serviceProvider;
    private DefaultHtmlAgilityPackCrawler _crawler;
    private string _entry;
    private string[] _pages;

    public enum Mode
    {
        Coupled8,
        Coupled64,
        Split64x8,
    }

    [Params(Mode.Coupled8, Mode.Coupled64, Mode.Split64x8)]
    public Mode Configuration;

    // Light payload => fetch-dominated; heavy payload => parse (HtmlDocument.Load) dominated.
    [Params(16, 300)]
    public int FillerNodes;

    [GlobalSetup]
    public async Task Setup()
    {
        _entry = $"http://localhost:{_port}/page/0";

        // Precompute every page once so the request handler returns a cached string; HTML generation
        // must not run on the hot path, where the in-process Kestrel host would steal CPU from the crawler.
        _pages = new string[_pageCount];
        for (var id = 0; id < _pageCount; id++)
            _pages[id] = BuildPage(id, FillerNodes);

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(_port));
        builder.Logging.ClearProviders();

        _host = builder.Build();
        _host.Use(async (context, next) =>
        {
            await Task.Delay(_latencyMs);
            await next(context);
        });
        _host.MapGet("/page/{id:int}", (int id) => Results.Content(_pages[id], "text/html"));

        await _host.StartAsync();

        var services = new ServiceCollection();
        var options = new CrawlerOptions
        {
            CrawlDelay = 0,
            MaxPages = int.MaxValue,
            RespectMetaRobots = false,
            RespectRobotsTxt = false,
        };

        switch (Configuration)
        {
            case Mode.Coupled8:
                options.Concurrency = 8;
                break;
            case Mode.Coupled64:
                options.Concurrency = 64;
                break;
            case Mode.Split64x8:
                options.Concurrency = 64;
                options.ParseConcurrency = 8;
                break;
        }

        services.AddHtmlAgilityPackCrawler(options);
        services.AddSingleton<ILogger>(NullLogger.Instance);

        _serviceProvider = services.BuildServiceProvider();
        _crawler = _serviceProvider.GetRequiredService<DefaultHtmlAgilityPackCrawler>();
    }

    [Benchmark]
    public async Task Crawl()
    {
        await _crawler.Start(_entry, CancellationToken.None);
    }

    [GlobalCleanup]
    public async Task Cleanup()
    {
        await _serviceProvider.DisposeAsync();
        await _host.DisposeAsync();
    }

    private static string BuildPage(int id, int fillerNodes)
    {
        var sb = new StringBuilder(48 * 1024);
        sb.Append("<!doctype html><html><head><link rel=\"canonical\" href=\"/page/").Append(id).Append("\" /></head><body>");

        for (var i = 0; i < fillerNodes; i++)
            sb.Append("<div class=\"row\"><span>Item ").Append(i).Append("</span><p>Lorem ipsum dolor sit amet consectetur adipiscing.</p></div>");

        for (var k = 1; k <= _fanout; k++)
        {
            var child = id * _fanout + k;
            if (child < _pageCount)
                sb.Append("<a href=\"/page/").Append(child).Append("\">next ").Append(child).Append("</a>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }
}
