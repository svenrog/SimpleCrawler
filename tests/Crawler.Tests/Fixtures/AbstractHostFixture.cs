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

public abstract class AbstractHostFixture : IAsyncDisposable
{
    public readonly ServiceProvider ServiceProvider;
    public readonly WebApplication Host;
    public readonly CancellationTokenSource CancellationSource;
    public readonly List<string> Links;

    public AbstractHostFixture()
    {
        var services = new ServiceCollection();
        var options = new CrawlerOptions
        {
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

    public async ValueTask DisposeAsync()
    {
        await Host.StopAsync(CancellationSource.Token);
        await Host.DisposeAsync();

        CancellationSource.Dispose();
        ServiceProvider.Dispose();

        GC.SuppressFinalize(this);
    }
}
