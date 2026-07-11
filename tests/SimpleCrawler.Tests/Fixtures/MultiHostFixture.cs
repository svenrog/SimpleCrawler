using SimpleCrawler.Core;
using SimpleCrawler.HtmlAgilityPack;
using SimpleCrawler.Tests.Common.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace SimpleCrawler.Tests.Fixtures;

/// <summary>
/// Two in-scope hosts that cross-link to each other and to a third, unserved, out-of-scope host, so the
/// crawl can be asserted to stay within exactly the hosts it was given as entries.
/// </summary>
public sealed class MultiHostFixture : IAsyncDisposable
{
    private static readonly TimeSpan _shutdownTimeout = TimeSpan.FromSeconds(10);

    public const string HostA = "http://localhost:5271/";
    public const string HostB = "http://localhost:5272/";
    public const string HostExternal = "http://localhost:5279/";

    public readonly ServiceProvider ServiceProvider;
    public readonly CancellationTokenSource CancellationSource = new();

    private readonly IReadOnlyList<WebApplication> _hosts;

    public MultiHostFixture()
    {
        var services = new ServiceCollection();
        services.AddHtmlAgilityPackCrawler(new CrawlerOptions
        {
            CrawlDelay = 0,
            Concurrency = 4,
            RespectMetaRobots = false,
            RespectRobotsTxt = false,
            EnableSitemapDiscovery = false,
        });

        ServiceProvider = services.BuildServiceProvider();

        _hosts =
        [
            CreateHost(HostA, new Dictionary<string, string>
            {
                ["/"] = Page($"<a href=\"/a-1\">a1</a><a href=\"{HostB}b-1\">b1</a><a href=\"{HostExternal}ext\">ext</a>"),
                ["/a-1"] = Page("a-1"),
            }),
            CreateHost(HostB, new Dictionary<string, string>
            {
                ["/"] = Page("<a href=\"/b-1\">b1</a>"),
                ["/b-1"] = Page("b-1"),
            }),
        ];

        foreach (var host in _hosts)
            host.StartAsync(CancellationSource.Token).AwaitSync();
    }

    public DefaultHtmlAgilityPackCrawler CreateCrawler()
        => ServiceProvider.GetRequiredService<DefaultHtmlAgilityPackCrawler>();

    private static string Page(string body) => $"<html><body>{body}</body></html>";

    private static WebApplication CreateHost(string url, IReadOnlyDictionary<string, string> pages)
    {
        Environment.SetEnvironmentVariable("ASPNETCORE_URLS", url);

        var builder = WebApplication.CreateSlimBuilder();
        var app = builder.Build();

        app.MapGet("/{**path}", (HttpContext context) =>
        {
            var path = context.Request.Path.Value ?? "/";
            var html = pages.TryGetValue(path, out var page) ? page : Page(string.Empty);
            return Results.Content(html, "text/html");
        });

        return app;
    }

    public async ValueTask DisposeAsync()
    {
        // Bound the whole shutdown so neither a stalled host nor an unbounded ServiceProvider disposal can
        // pin the test host once the runner (or the IDE "Stop" button) has given up on the run.
        CancellationSource.CancelAfter(_shutdownTimeout);

        try
        {
            await TeardownGuard.RunBounded(StopAndDisposeHosts, _shutdownTimeout);
        }
        finally
        {
            CancellationSource.Dispose();
        }
    }

    private async Task StopAndDisposeHosts()
    {
        foreach (var host in _hosts)
        {
            try
            {
                await host.StopAsync(CancellationSource.Token);
            }
            catch
            {
                // Swallowed deliberately: a host that throws must not skip the ServiceProvider or the rest.
            }

            try
            {
                await host.DisposeAsync();
            }
            catch
            {
                // As above: keep going so ServiceProvider disposal is always reached.
            }
        }

        await ServiceProvider.DisposeAsync();
    }
}
