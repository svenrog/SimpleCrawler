using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace SimpleCrawler.Core.Collectors;

public static class CollectorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the built-in collectors implied by <paramref name="options"/> — currently the
    /// <see cref="PageSignalsCollector"/> when <see cref="CrawlerOptions.CapturePageSignals"/> is on.
    /// Callers can register additional <see cref="ICrawlCollector"/>s before or after; every backend
    /// resolves the full set. Idempotent per implementation type, so wiring several backends into one
    /// container (as the tests do) never double-registers a built-in collector.
    /// </summary>
    public static IServiceCollection AddCrawlCollectors(this IServiceCollection services, CrawlerOptions options)
    {
        if (options.CapturePageSignals)
            services.TryAddEnumerable(ServiceDescriptor.Singleton<ICrawlCollector, PageSignalsCollector>());

        return services;
    }
}
