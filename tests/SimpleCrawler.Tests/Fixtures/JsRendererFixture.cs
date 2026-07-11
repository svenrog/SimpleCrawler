using Microsoft.Extensions.DependencyInjection;
using SimpleCrawler.Core;
using SimpleCrawler.Js.Abstractions;
using SimpleCrawler.Js.Jint;
using SimpleCrawler.Js.V8;
using SimpleCrawler.Tests.Models;

namespace SimpleCrawler.Tests.Fixtures;

/// <summary>
/// Holds a single ServiceProvider with both JS engines registered so the ~90 host-less renderer tests
/// resolve a shared keyed <see cref="IJsEngineFactory"/> instead of each building its own container. The
/// factory is only a lightweight producer - every render still spins a fresh engine, preserving the
/// deliberate fresh-engine-per-page policy.
/// </summary>
public sealed class JsRendererFixture : IDisposable
{
    private readonly ServiceProvider _provider;

    public JsRendererFixture()
    {
        var services = new ServiceCollection();
        services.AddJintCrawler(new CrawlerOptions());
        services.AddV8Crawler(new CrawlerOptions());
        _provider = services.BuildServiceProvider();
    }

    public IJsEngineFactory GetFactory(JsEngine engine)
    {
        var key = engine == JsEngine.V8 ? "js-v8" : "js-jint";
        return _provider.GetRequiredKeyedService<IJsEngineFactory>(key);
    }

    public void Dispose() => _provider.Dispose();
}
