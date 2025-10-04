using Crawler.AngleSharp;
using Crawler.Core;
using Crawler.Tests.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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
        var options = CreateOptions();

        services.AddAngleSharpCrawler(options);
        services.AddHtmlAgilityPackCrawler(options);
        services.AddPlaywrightCrawler(options);

        services.AddSingleton<ILogger>(NullLogger.Instance);
        services.AddScoped<CancellationTokenSource>();

        ServiceProvider = services.BuildServiceProvider();
        CancellationSource = ServiceProvider.GetRequiredService<CancellationTokenSource>();

        Host = CreateHost();
        Host.StartAsync(CancellationSource.Token).AwaitSync();

        Links = GetLinks();
    }

    protected virtual CrawlerOptions CreateOptions()
    {
        return new CrawlerOptions
        {
            CrawlDelay = 0,
            Parallelism = 4,
            RespectMetaRobots = false,
            RespectRobotsTxt = false,
        };
    }

    protected abstract WebApplication CreateHost();

    protected abstract List<string> GetLinks();

    public async ValueTask DisposeAsync()
    {
        await Host.StopAsync(CancellationSource.Token);
        await Host.DisposeAsync();
        await ServiceProvider.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
