using System.Collections.Concurrent;
using System.Net;
using Crawler.Core.Helpers;
using Microsoft.Extensions.Options;

namespace Crawler.Core.Proxy;

public sealed class ProxyHandlerProvider : IProxyClientProvider, IDisposable
{
    private readonly CrawlerOptions _options;
    private readonly ConcurrentDictionary<ProxyInfo, Lazy<HttpClient>> _clients = new();

    public ProxyHandlerProvider(IOptions<CrawlerOptions> options)
    {
        _options = options.Value;
    }

    public HttpClient ClientFor(ProxyInfo proxy)
    {
        var lazy = _clients.GetOrAdd(
            proxy,
            key => new Lazy<HttpClient>(() => Build(key), LazyThreadSafetyMode.ExecutionAndPublication));

        return lazy.Value;
    }

    private HttpClient Build(ProxyInfo proxy)
    {
        var handler = ConfigurationHelper.CreatePrimaryHandler(_options);

        switch (proxy.Protocol)
        {
            case ProxyProtocol.Http:
            case ProxyProtocol.Https:
                handler.Proxy = new WebProxy(proxy.ToUri());
                if (proxy.HasCredentials)
                    handler.Credentials = new NetworkCredential(proxy.Username, proxy.Password!);
                break;
            case ProxyProtocol.Socks4:
            case ProxyProtocol.Socks5:
                handler.ConnectCallback = (context, cancellationToken) =>
                    SocksConnect.ConnectAsync(context, proxy, cancellationToken);
                break;
        }

        return new HttpClient(handler, disposeHandler: true);
    }

    public void Dispose()
    {
        foreach (var lazy in _clients.Values)
        {
            if (lazy.IsValueCreated)
                lazy.Value.Dispose();
        }

        _clients.Clear();
    }
}
