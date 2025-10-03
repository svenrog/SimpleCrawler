using AngleSharp;
using Crawler.Core;
using Crawler.Tests.Common.Crawlers;
using Crawler.Tests.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Crawler.Tests.Fixtures;

public abstract class AbstractHostFixture : IDisposable
{
    public const string HostName = "http://localhost:5234/";
    public static readonly Uri HostUri = new(HostName);

    public readonly ServiceProvider ServiceProvider;
    public readonly WebApplication Host;
    public readonly CancellationTokenSource CancellationSource;
    public readonly List<string> Links;

    public AbstractHostFixture()
    {
        var services = new ServiceCollection();
        var options = new CrawlerOptions
        {
            Entry = HostName,
            CrawlDelay = 0,
            Parallelism = 4,
        };

        services.AddSingleton(Options.Create(options));
        services.AddSingleton<HttpClient>();
        services.AddSingleton(Configuration.Default.WithDefaultLoader());
        services.AddSingleton<ILogger>(NullLogger.Instance);

        services.AddScoped<HtmlAgilityPackCrawler>();
        services.AddScoped<AngleSharpCrawler>();
        services.AddScoped<PlaywrightCrawler>();

        CancellationSource = new CancellationTokenSource();
        ServiceProvider = services.BuildServiceProvider();
        Host = CreateHost();
        Host.StartAsync(CancellationSource.Token).AwaitSync();

        Links = GetLinks();
    }

    protected abstract WebApplication CreateHost();

    protected abstract List<string> GetLinks();

    public void Dispose()
    {
        Host.StopAsync(CancellationSource.Token).AwaitSync();
        Host.DisposeSync();

        CancellationSource.Dispose();
        ServiceProvider.Dispose();

        GC.SuppressFinalize(this);
    }
}
