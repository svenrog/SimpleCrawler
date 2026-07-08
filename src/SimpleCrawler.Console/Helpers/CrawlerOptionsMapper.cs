using SimpleCrawler.Core;
using SimpleCrawler.Core.Proxy;
using SimpleCrawler.Core.Retry;
using SimpleCrawler.Core.Throttling;

namespace SimpleCrawler.Console.Helpers;

public static class CrawlerOptionsMapper
{
    public static CrawlerOptions Map(Options options, ProxyPoolOptions? proxy)
    {
        return new CrawlerOptions
        {
            MaxPages = options.MaxPages,
            Concurrency = options.Concurrency,
            ParseConcurrency = options.ParseConcurrency,
            CrawlDelay = options.CrawlDelay,
            RespectMetaRobots = options.RespectRobots,
            RespectRobotsTxt = options.RespectRobots,
            BrowserProfile = ProfileMapper.Map(options),
            ProxyPool = proxy,
            Retry = new RetryOptions
            {
                MaxRetries = options.Retries,
                BaseDelay = TimeSpan.FromMilliseconds(options.RetryDelay),
                MaxDelay = TimeSpan.FromMilliseconds(options.MaxRetryDelay),
                AttemptTimeout = TimeSpan.FromMilliseconds(options.AttemptTimeout),
            },
            Throttling = new ThrottleOptions
            {
                Enabled = options.AdaptiveThrottle,
            },
        };
    }
}
