using SimpleCrawler.Core.Models;

namespace SimpleCrawler.Core.Helpers;

/// <summary>
/// Builds the backend-neutral <see cref="ResponseSignal"/> (status, size, type, and — when captured —
/// headers and cookie names) from a raw <see cref="HttpResponseMessage"/>. Shared by every backend
/// that fetches via <see cref="HttpClient"/>: the static crawlers and the JS renderer's page-shell fetch.
/// </summary>
public static class HttpSignalCollector
{
    /// <summary>
    /// Normalizes <paramref name="response"/> into a <see cref="ResponseSignal"/>. Headers and cookie
    /// names are collected only when <paramref name="captureSignals"/> is set, so a plain crawl pays for
    /// nothing beyond status/size/type.
    /// </summary>
    public static ResponseSignal ToResponseSignal(HttpResponseMessage response, bool captureSignals)
    {
        return new ResponseSignal
        {
            StatusCode = (int)response.StatusCode,
            ContentLength = response.Content.Headers.ContentLength,
            ContentType = response.Content.Headers.ContentType?.MediaType,
            Headers = captureSignals ? CollectHeaders(response) : ResponseSignal.EmptyHeaders,
            CookieNames = captureSignals ? CollectCookieNames(response) : [],
        };
    }

    /// <summary>Response headers, lower-cased keys to single joined values.</summary>
    public static Dictionary<string, string> CollectHeaders(HttpResponseMessage response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in response.Headers.Concat(response.Content.Headers))
            headers[header.Key.ToLowerInvariant()] = string.Join(ResponseSignal.HeaderValueSeparator, header.Value);

        return headers;
    }

    /// <summary>Names of cookies set via <c>Set-Cookie</c> (values are intentionally dropped).</summary>
    public static List<string> CollectCookieNames(HttpResponseMessage response)
    {
        var names = new List<string>();
        if (!response.Headers.TryGetValues("Set-Cookie", out var values))
            return names;

        foreach (var value in values)
        {
            var pair = value.Split(';', 2)[0];
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            if (equals > 0)
                names.Add(pair[..equals].Trim());
        }

        return names;
    }
}
