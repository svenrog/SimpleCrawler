using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace Crawler.Js.Network;

public sealed class JsHttp
{
    private readonly HttpClient _client;
    private readonly Uri _baseUri;
    private readonly ILogger _logger;
    private readonly CancellationToken _cancellationToken;

    public JsHttp(HttpClient client, Uri baseUri, ILogger logger, CancellationToken cancellationToken)
    {
        _client = client;
        _baseUri = baseUri;
        _logger = logger;
        _cancellationToken = cancellationToken;
    }

    // (url, method, headersJson, body) — invoked from the JS fetch/XMLHttpRequest shims. The request is
    // issued synchronously to fit the single-threaded drain loop; the JS side wraps it in a resolved Promise.
    public JsHttpResponse request(params object?[] args)
    {
        var url = args.Length > 0 ? args[0]?.ToString() : null;
        if (string.IsNullOrEmpty(url))
            return new JsHttpResponse { error = "fetch called without a URL" };

        var method = args.Length > 1 ? args[1]?.ToString() : null;
        var headersJson = args.Length > 2 ? args[2]?.ToString() : null;
        var body = args.Length > 3 ? args[3]?.ToString() : null;

        if (!Uri.TryCreate(_baseUri, url, out var absolute))
            return new JsHttpResponse { error = $"invalid fetch URL '{url}'" };

        try
        {
            using var message = BuildRequest(absolute, method, headersJson, body);

            _logger.LogDebug("Render fetch {method} {url}", message.Method.Method, absolute);

            using var response = _client.Send(message, _cancellationToken);
            var content = response.Content.ReadAsStringAsync(_cancellationToken).GetAwaiter().GetResult();

            return new JsHttpResponse
            {
                status = (int)response.StatusCode,
                statusText = response.ReasonPhrase ?? string.Empty,
                url = absolute.ToString(),
                body = content,
                headersJson = SerializeHeaders(response),
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Render fetch failed for '{url}': {message}", absolute, ex.Message);
            return new JsHttpResponse { url = absolute.ToString(), error = ex.Message };
        }
    }

    private static HttpRequestMessage BuildRequest(Uri absolute, string? method, string? headersJson, string? body)
    {
        var verb = string.IsNullOrEmpty(method) ? HttpMethod.Get : new HttpMethod(method.ToUpperInvariant());
        var message = new HttpRequestMessage(verb, absolute);

        if (!string.IsNullOrEmpty(body) && verb != HttpMethod.Get && verb != HttpMethod.Head)
            message.Content = new StringContent(body, Encoding.UTF8);

        if (!string.IsNullOrEmpty(headersJson))
            ApplyHeaders(message, headersJson);

        return message;
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
