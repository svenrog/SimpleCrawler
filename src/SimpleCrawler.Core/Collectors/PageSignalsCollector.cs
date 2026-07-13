using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;
using System.Text.Json;

namespace SimpleCrawler.Core.Collectors;

/// <summary>
/// The built-in collector behind <see cref="CrawlerOptions.CapturePageSignals" />: merges the HTTP half (headers,
/// cookie names) from <see cref="OnResponse"/> and the DOM half (script sources, meta tags, JSON-LD) onto a single
/// <see cref="UrlReport.Signals"/> instance. It is the sole owner of what a "signal" is: the DOM half is expressed
/// once as an <see cref="IPageDom"/> walk for the static backends (<see cref="OnDocument"/>) and once as a JS
/// fragment (<see cref="DomScript"/>) the rendered backends run in-page (<see cref="OnRendered"/>) — no backend
/// carries any signal-specific code.
/// </summary>
public sealed class PageSignalsCollector : IRenderedDomCollector, IStaticDomCollector
{
    /// <summary>The stable key under which this collector's rendered-extraction slice is returned.</summary>
    public string Key => "signals";

    /// <summary>
    /// The in-page fragment mirroring <see cref="OnDocument"/>: it collects script sources, meta tags, and
    /// JSON-LD blocks into the same shape <see cref="PageSignalsParser.Read"/> reads back. Index-based loops
    /// (not <c>for…of</c>) keep it runnable on the in-process JS DOM as well as a real browser.
    /// </summary>
    public string DomScript => """
        () => {
            var scriptSources = [], jsonLdBlocks = [], metaTags = {};
            var scripts = document.querySelectorAll('script');
            for (var i = 0; i < scripts.length; i++) {
                var s = scripts[i];
                var src = s.getAttribute('src');
                if (src) { scriptSources.push(src); }
                else if ((s.getAttribute('type') || '').toLowerCase() === 'application/ld+json') {
                    var text = (s.textContent || '').trim();
                    if (text) jsonLdBlocks.push(text);
                }
            }
            var metas = document.querySelectorAll('meta');
            for (var j = 0; j < metas.length; j++) {
                var m = metas[j];
                var name = m.getAttribute('name') || m.getAttribute('property');
                var content = m.getAttribute('content');
                if (name && content !== null) metaTags[name] = content;
            }
            return { scriptSources: scriptSources, metaTags: metaTags, jsonLdBlocks: jsonLdBlocks };
        }
        """;

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

    public ValueTask OnDocument(UrlReport report, IPageDom dom, string resolvedUrl)
    {
        var signals = report.Signals ??= new PageSignals();

        foreach (var script in dom.QueryAll("script"))
        {
            var src = script.GetAttribute("src");
            if (!string.IsNullOrEmpty(src))
            {
                signals.ScriptSources.Add(src);
            }
            else if (string.Equals(script.GetAttribute("type"), "application/ld+json", StringComparison.OrdinalIgnoreCase))
            {
                var jsonLd = script.Text.Trim();
                if (jsonLd.Length > 0)
                    signals.JsonLdBlocks.Add(jsonLd);
            }
        }

        foreach (var meta in dom.QueryAll("meta"))
        {
            var name = meta.GetAttribute("name") ?? meta.GetAttribute("property");
            var content = meta.GetAttribute("content");
            if (name is not null && content is not null)
                signals.MetaTags[name] = content;
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnRendered(UrlReport report, JsonElement result, string resolvedUrl)
    {
        var dom = PageSignalsParser.Read(result);
        var signals = report.Signals ??= new PageSignals();
        signals.ScriptSources = dom.ScriptSources;
        signals.MetaTags = dom.MetaTags;
        signals.JsonLdBlocks = dom.JsonLdBlocks;

        return ValueTask.CompletedTask;
    }
}
