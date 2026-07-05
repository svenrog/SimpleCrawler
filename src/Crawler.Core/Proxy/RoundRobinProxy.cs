using System.Collections.Concurrent;
using System.Net;

namespace Crawler.Core.Proxy;

public class RoundRobinProxy : IWebProxy
{
    private readonly ConcurrentQueue<Uri> _proxyAddresses;

    public RoundRobinProxy(IEnumerable<Uri> proxyAddresses)
    {
        if (proxyAddresses == null || !proxyAddresses.Any())
            throw new ArgumentException("Proxy addresses cannot be null or empty.", nameof(proxyAddresses));

        _proxyAddresses = new ConcurrentQueue<Uri>(proxyAddresses);
    }

    public ICredentials? Credentials { get; set; }

    public Uri? GetProxy(Uri destination)
    {
        if (_proxyAddresses.TryDequeue(out Uri? proxy))
        {
            _proxyAddresses.Enqueue(proxy);
            return proxy;
        }

        return null;
    }

    public virtual bool IsBypassed(Uri host)
    {
        return false;
    }
}
