using Microsoft.Extensions.Logging;

namespace SimpleCrawler.Core.Proxy;

public sealed class ProxyRoutingHandler : HttpMessageHandler
{
    private readonly IProxyClientProvider _clients;
    private readonly ProxyRetryExecutor _retry;
    private readonly ILogger<ProxyRoutingHandler> _logger;

    public ProxyRoutingHandler(IProxyPool pool, IProxyClientProvider clients, ProxyPoolOptions options, ILogger<ProxyRoutingHandler> logger)
    {
        _clients = clients;
        _retry = new ProxyRetryExecutor(pool, options.MaxRetries);
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        // Kept across attempts so an exhausted retry budget can surface the final proxy's HTTP error
        // response instead of a synthetic exception. Any earlier failed response is stale and disposed
        // at the top of the next attempt.
        HttpResponseMessage? lastResponse = null;

        var result = await _retry.ExecuteAsync<HttpResponseMessage?>(
            async (proxy, token) =>
            {
                lastResponse?.Dispose();
                lastResponse = null;

                var inner = _clients.ClientFor(proxy!);
                var clone = await CloneRequestAsync(request).ConfigureAwait(false);

                HttpResponseMessage response;
                try
                {
                    response = await inner.SendAsync(clone, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                    clone.Dispose();
                    throw;
                }
                catch (Exception ex)
                {
                    clone.Dispose();
                    lastError = ex;
                    _logger.LogDebug("Proxy {proxy} failed: {message}", proxy, ex.Message);
                    return ProxyAttempt<HttpResponseMessage?>.Failed(ProxyFailureKind.Connection);
                }

                var kind = ProxyFailureClassifier.Classify((int)response.StatusCode);
                if (kind is null)
                    return ProxyAttempt<HttpResponseMessage?>.Ok(response);

                lastResponse = response;
                return ProxyAttempt<HttpResponseMessage?>.Failed(kind.Value, response);
            },
            () => lastResponse ?? throw new HttpRequestException("All proxies failed for request.", lastError),
            cancellationToken);

        return result!;
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
