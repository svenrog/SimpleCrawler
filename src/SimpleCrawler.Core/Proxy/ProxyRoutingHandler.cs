using Microsoft.Extensions.Logging;
using System.Net;

namespace Crawler.Core.Proxy;

public sealed class ProxyRoutingHandler : HttpMessageHandler
{
    private readonly IProxyPool _pool;
    private readonly IProxyClientProvider _clients;
    private readonly ProxyPoolOptions _options;
    private readonly ILogger<ProxyRoutingHandler> _logger;

    public ProxyRoutingHandler(IProxyPool pool, IProxyClientProvider clients, ProxyPoolOptions options, ILogger<ProxyRoutingHandler> logger)
    {
        _pool = pool;
        _clients = clients;
        _options = options;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            var proxy = _pool.Acquire() ?? throw new ProxyPoolExhaustedException("No healthy proxies remain (below configured cutoff).");
            var inner = _clients.ClientFor(proxy);
            var clone = await CloneRequestAsync(request).ConfigureAwait(false);

            HttpResponseMessage? response;
            try
            {
                response = await inner.SendAsync(clone, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                clone.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                clone.Dispose();
                lastError = ex;
                _logger.LogDebug("Proxy {proxy} failed on attempt {attempt}: {message}", proxy, attempt + 1, ex.Message);
                _pool.ReportFailure(proxy, ProxyFailureKind.Connection);
                continue;
            }

            var kind = Classify(response.StatusCode);

            if (kind is null)
            {
                _pool.ReportSuccess(proxy);
                return response;
            }

            _pool.ReportFailure(proxy, kind.Value);

            if (attempt == _options.MaxRetries)
                return response;

            response.Dispose();
        }

        throw new HttpRequestException("All proxies failed for request.", lastError);
    }

    private static ProxyFailureKind? Classify(HttpStatusCode status)
    {
        var code = (int)status;
        if (code == 407)
            return ProxyFailureKind.ProxyAuth;
        if (code == 429)
            return ProxyFailureKind.Http429;
        if (code is 500 or 502 or 503 or 504)
            return ProxyFailureKind.Http5xx;
        return null;
    }

    private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (request.Content is not null)
        {
            var bytes = await request.Content.ReadAsByteArrayAsync(CancellationToken.None).ConfigureAwait(false);
            var content = new ByteArrayContent(bytes);
            foreach (var header in request.Content.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            clone.Content = content;
        }

        return clone;
    }

    protected override void Dispose(bool disposing)
    {
        // Inner handlers are owned by the singleton ProxyHandlerProvider; leaving this empty keeps
        // HttpClientFactory handler rotation (which disposes primary handlers) harmless.
    }
}
