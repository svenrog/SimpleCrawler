using Microsoft.Extensions.Logging;
using SimpleCrawler.Js.Services;
using System.Text;
using System.Text.Json;

namespace SimpleCrawler.Js.Network;

public sealed class JsHttp
{
    private readonly HttpClient _client;
    private readonly Uri _baseUri;
    private readonly ILogger _logger;
    private readonly RenderFetchCache _cache;
    private readonly CancellationToken _cancellationToken;

    internal JsHttp(HttpClient client, Uri baseUri, ILogger logger, RenderFetchCache cache, CancellationToken cancellationToken)
    {
        _client = client;
        _baseUri = baseUri;
        _logger = logger;
        _cache = cache;
        _cancellationToken = cancellationToken;
    }

    public JsHttp(HttpClient client, Uri baseUri, ILogger logger, CancellationToken cancellationToken)
        : this(client, baseUri, logger, new RenderFetchCache(), cancellationToken)
    {
    }

    /// <summary>
    /// (url, method, headersJson, body) — invoked from the JS fetch/XMLHttpRequest shims. The request is
    /// issued synchronously to fit the single-threaded drain loop; the JS side wraps it in a resolved Promise.
    /// </summary>
    public JsHttpResponse request(params object?[] args)
    {
        try
        {
            return RequestCore(args);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Render fetch threw before send: {message}", ex.Message);
            return new JsHttpResponse { error = ex.Message };
        }
    }

    /// <summary>
    /// Delegate-friendly entry point embedded as the <c>__httpRequest</c> global. ClearScript's V8 backend
    /// cannot reflectively invoke a host object's instance method under NativeAOT (it throws while binding the
    /// call), so the fetch bridge is embedded as a plain variadic function instead and the whole response
    /// crosses back as a JSON string — no host-object member access on either the call or the result.
    /// </summary>
    public object? requestJson(params object?[] args) => SerializeResponse(request(args));

    private static string SerializeResponse(JsHttpResponse response)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("status", response.status);
            writer.WriteString("statusText", response.statusText);
            writer.WriteString("url", response.url);
            writer.WriteString("body", response.body);
            writer.WriteString("headersJson", response.headersJson);
            if (response.error is not null)
                writer.WriteString("error", response.error);

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private JsHttpResponse RequestCore(params object?[] args)
    {
        var url = args.Length > 0 ? args[0]?.ToString() : null;
        if (string.IsNullOrEmpty(url))
            return new JsHttpResponse { error = "fetch called without a URL" };

        var method = args.Length > 1 ? args[1]?.ToString() : null;
        var headersJson = args.Length > 2 ? args[2]?.ToString() : null;
        var body = args.Length > 3 ? args[3]?.ToString() : null;

        if (!Uri.TryCreate(_baseUri, url, out var absolute))
            return new JsHttpResponse { error = $"invalid fetch URL '{url}'" };

        var verb = string.IsNullOrEmpty(method) ? HttpMethod.Get : new HttpMethod(method.ToUpperInvariant());
        var absoluteUrl = absolute.ToString();

        // Next.js App Router speculatively prefetches every visible link's RSC payload. Those responses feed
        // the client router cache for future navigations, not the current page's DOM, so for link extraction
        // they are pure network overhead — and each carries a per-navigation cache-buster, so they never
        // dedupe either. Answer with an empty 204 (no RSC content-type) so the framework's best-effort
        // prefetch path treats it as a non-RSC response and skips it, without a request leaving the process.
        if (HasHeader(headersJson, "Next-Router-Prefetch"))
            return new JsHttpResponse { status = 204, statusText = "No Content", url = absoluteUrl };

        // Only safe/idempotent methods are deduplicated; a POST etc. may mutate server state, so it always
        // reaches the network. A byte-identical GET/HEAD is served from the cross-page cache unless the
        // caller explicitly opted out (Cache-Control: no-store/no-cache, Pragma: no-cache).
        var cacheable = (verb == HttpMethod.Get || verb == HttpMethod.Head) && !RequestForbidsCache(headersJson);
        if (cacheable && _cache.TryGet(verb.Method, absoluteUrl, headersJson, out var hit))
        {
            _logger.LogDebug("Render fetch (cache hit) {method} {url}", verb.Method, absolute);
            return hit;
        }

        try
        {
            using var message = BuildRequest(absolute, verb, headersJson, body);

            _logger.LogDebug("Render fetch {method} {url}", verb.Method, absolute);

            using var response = _client.Send(message, _cancellationToken);
            var content = response.Content.ReadAsStringAsync(_cancellationToken).GetAwaiter().GetResult();

            var result = new JsHttpResponse
            {
                status = (int)response.StatusCode,
                statusText = response.ReasonPhrase ?? string.Empty,
                url = absoluteUrl,
                body = content,
                headersJson = SerializeHeaders(response),
            };

            // Only memoize responses the origin permits caching of, and only successful ones — replaying a
            // cached 429/5xx (or a body the server marked no-store/private) for the rest of the crawl is worse
            // than re-fetching.
            if (cacheable && response.IsSuccessStatusCode && ResponseAllowsCache(response))
                _cache.Store(verb.Method, absoluteUrl, headersJson, result);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Render fetch failed for '{url}': {message}", absolute, ex.Message);
            return new JsHttpResponse { url = absoluteUrl, error = ex.Message };
        }
    }

    private static HttpRequestMessage BuildRequest(Uri absolute, HttpMethod verb, string? headersJson, string? body)
    {
        var message = new HttpRequestMessage(verb, absolute);

        if (!string.IsNullOrEmpty(body) && verb != HttpMethod.Get && verb != HttpMethod.Head)
            message.Content = new StringContent(body, Encoding.UTF8);

        if (!string.IsNullOrEmpty(headersJson))
            ApplyHeaders(message, headersJson);

        return message;
    }

    private static bool HasHeader(string? headersJson, string name)
        => GetHeaderValue(headersJson, name) is not null;

    private static string? GetHeaderValue(string? headersJson, string name)
    {
        if (string.IsNullOrEmpty(headersJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(headersJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return null;

            foreach (var header in document.RootElement.EnumerateObject())
            {
                if (string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
                    return header.Value.ValueKind == JsonValueKind.String ? header.Value.GetString() : header.Value.ToString();
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    private static bool RequestForbidsCache(string? headersJson)
    {
        var cacheControl = GetHeaderValue(headersJson, "Cache-Control");
        if (cacheControl is not null &&
            (cacheControl.Contains("no-store", StringComparison.OrdinalIgnoreCase) ||
             cacheControl.Contains("no-cache", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var pragma = GetHeaderValue(headersJson, "Pragma");
        return pragma is not null && pragma.Contains("no-cache", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ResponseAllowsCache(HttpResponseMessage response)
    {
        var cacheControl = response.Headers.CacheControl;
        if (cacheControl is not null &&
            (cacheControl.NoStore || cacheControl.NoCache || cacheControl.Private || cacheControl.MaxAge == TimeSpan.Zero))
        {
            return false;
        }

        foreach (var vary in response.Headers.Vary)
        {
            if (vary == "*")
                return false;
        }

        return true;
    }

    private static void ApplyHeaders(HttpRequestMessage message, string headersJson)
    {
        using var document = JsonDocument.Parse(headersJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
            return;

        foreach (var header in document.RootElement.EnumerateObject())
        {
            var value = header.Value.ValueKind == JsonValueKind.String ? header.Value.GetString() : header.Value.ToString();
            if (value is null)
                continue;

            if (!message.Headers.TryAddWithoutValidation(header.Name, value) && message.Content is not null)
            {
                message.Content.Headers.Remove(header.Name);
                message.Content.Headers.TryAddWithoutValidation(header.Name, value);
            }
        }
    }

    private static string SerializeHeaders(HttpResponseMessage response)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var header in response.Headers)
                writer.WriteString(header.Key, string.Join(", ", header.Value));

            foreach (var header in response.Content.Headers)
                writer.WriteString(header.Key, string.Join(", ", header.Value));

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
