using SimpleCrawler.Core.Models;

namespace SimpleCrawler.Core.Collectors;

/// <summary>
/// The built-in collector behind <see cref="CrawlerOptions.CapturePageSignals" />: merges the HTTP half (headers, cookie names)
/// from <see cref="OnResponse"/> and the DOM half (script sources, meta tags, JSON-LD) from
/// <see cref="OnDocument"/> onto a single <see cref="UrlReport.Signals"/> instance.
/// </summary>
public sealed class PageSignalsCollector : ICrawlCollector
{
    public void OnResponse(UrlReport report, ResponseSignal response)
    {
        if (response.Headers.Count == 0 && response.CookieNames.Count == 0)
            return;

        var signals = report.Signals ??= new PageSignals();

        if (response.Headers.Count > 0)
        {
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in response.Headers)
                headers[key] = value;
            signals.Headers = headers;
        }

        if (response.CookieNames.Count > 0)
            signals.CookieNames = [.. response.CookieNames];
    }

    public ValueTask OnDocument(UrlReport report, PageExtract extract, string resolvedUrl)
    {
        if (extract.Signals is { } dom)
        {
            var signals = report.Signals ??= new PageSignals();
            signals.ScriptSources = dom.ScriptSources;
            signals.MetaTags = dom.MetaTags;
            signals.JsonLdBlocks = dom.JsonLdBlocks;
        }

        return ValueTask.CompletedTask;
    }
}
