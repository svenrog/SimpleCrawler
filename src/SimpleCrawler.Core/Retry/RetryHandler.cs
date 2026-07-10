using Microsoft.Extensions.Logging;
using SimpleCrawler.Core.Proxy;

namespace SimpleCrawler.Core.Retry;

/// <summary>
/// Primary HTTP handler that routes every request through the shared RetryExecutor. A DelegatingHandler
/// cannot swap its inner transport per attempt, so this owns routing directly: with a pool it sends via
/// the per-proxy client; without one it sends via an owned direct transport and simply retries.
/// </summary>
public sealed class RetryHandler : HttpMessageHandler
{
    private readonly IProxyClientProvider? _clients;
    private readonly HttpMessageInvoker? _directInvoker;
    private readonly RetryExecutor _retry;
    private readonly ILogger<RetryHandler> _logger;

    public RetryHandler(RetryOptions retryOptions, IProxyClientProvider? clients, IProxyPool? pool, HttpMessageHandler? directInner, ILogger<RetryHandler> logger)
    {
        _clients = clients;
        _directInvoker = directInner is null ? null : new HttpMessageInvoker(directInner, disposeHandler: true);
        _retry = new RetryExecutor(retryOptions, pool);
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Exception? lastError = null;

        // Kept across attempts so an exhausted retry budget can surface the final HTTP error response
        // instead of a synthetic exception. Any earlier failed response is stale and disposed at the top
        // of the next attempt.
        HttpResponseMessage? lastResponse = null;

        var result = await _retry.ExecuteAsync(
            async (proxy, token) =>
            {
                lastResponse?.Dispose();
                lastResponse = null;

                var sender = proxy is not null ? _clients!.ClientFor(proxy) : _directInvoker!;
                var bytes = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(CancellationToken.None).ConfigureAwait(false);
                var clone = CloneRequest(request, bytes);

                HttpResponseMessage response;
                try
                {
                    response = await sender.SendAsync(clone, token).ConfigureAwait(false);
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
                    _logger.LogDebug("Request to '{url}' via '{proxy}' failed: {message}", request.RequestUri, ProxyLabel.Describe(proxy), ex.Message);
                    return RetryAttempt<HttpResponseMessage?>.Failed(RetryClassifier.Classify(ex));
                }

                var reason = RetryClassifier.Classify((int)response.StatusCode);
                if (reason is null)
                    return RetryAttempt<HttpResponseMessage?>.Ok(response);

                lastResponse = response;
                return RetryAttempt<HttpResponseMessage?>.Failed(reason.Value, response);
            },
            () => lastResponse ?? throw new HttpRequestException("All retry attempts failed for request.", lastError),
            cancellationToken);

        return result!;
    }

    /// <summary>
    /// The JS runtime issues blocking sends; without a synchronous override the base handler would throw
    /// NotSupportedException, so this mirrors SendAsync over the synchronous send path.
    /// </summary>
    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        HttpResponseMessage? lastResponse = null;

        var result = _retry.Execute<HttpResponseMessage?>(
            (proxy, token) =>
            {
                lastResponse?.Dispose();
                lastResponse = null;

                var sender = proxy is not null ? _clients!.ClientFor(proxy) : _directInvoker!;
                var bytes = request.Content?.ReadAsByteArrayAsync(CancellationToken.None).GetAwaiter().GetResult();
                var clone = CloneRequest(request, bytes);

                HttpResponseMessage response;
                try
                {
                    response = sender.Send(clone, token);
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
                    _logger.LogDebug("Request to '{url}' via '{proxy}' failed: {message}", request.RequestUri, ProxyLabel.Describe(proxy), ex.Message);
                    return RetryAttempt<HttpResponseMessage?>.Failed(RetryClassifier.Classify(ex));
                }

                var reason = RetryClassifier.Classify((int)response.StatusCode);
                if (reason is null)
                    return RetryAttempt<HttpResponseMessage?>.Ok(response);

                lastResponse = response;
                return RetryAttempt<HttpResponseMessage?>.Failed(reason.Value, response);
            },
            () => lastResponse ?? throw new HttpRequestException("All retry attempts failed for request.", lastError),
            cancellationToken);

        return result!;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request, byte[]? contentBytes)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (contentBytes is not null && request.Content is not null)
        {
            var content = new ByteArrayContent(contentBytes);
            foreach (var header in request.Content.Headers)
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            clone.Content = content;
        }

        return clone;
    }

    protected override void Dispose(bool disposing)
    {
        // Per-proxy clients are owned by the singleton ProxyHandlerProvider, so they are left alone
        // (HttpClientFactory rotation disposes primary handlers and would otherwise tear them down).
        // The direct transport, in contrast, is ours to dispose.
        if (disposing)
            _directInvoker?.Dispose();
    }
}
