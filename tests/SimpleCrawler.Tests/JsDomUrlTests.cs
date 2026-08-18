using SimpleCrawler.Js.Models;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// URL resolution and the URL/URLSearchParams pair. Both are read rather than called: a page asks what a
/// reference resolves to, or builds a request out of a URL object, and a wrong answer here leaves no trace in
/// the log — the render simply fetches something the page did not ask for, or nothing at all.
/// </summary>
[Collection("Crawler")]
public class JsDomUrlTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsDomUrlTests(JsRendererFixture fixture) : base(fixture)
    {
    }

    // A scheme-relative reference carries its own authority and borrows only the base's scheme. Resolved as a
    // path it becomes a request to the page's own host whose first segment is somebody else's hostname, which
    // 404s — and a loader that derives its CDN prefix as "//" + host is a common shape, so the chunk the page
    // is waiting on never arrives.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_ASchemeRelativeReferenceKeepsItsOwnHost(JsEngine engine)
    {
        const string html = """
            <html><body>
            <a id="anchor" href="//cdn.example.net/js/loader.js">x</a>
            <script>
            var script = document.createElement('script');
            script.src = '//cdn.example.net/chunk.js';
            var probe = document.createElement('a');
            probe.setAttribute('href', '/probe'
              + '?anchor=' + document.getElementById('anchor').href
              + '&script=' + script.src
              + '&attribute=' + script.getAttribute('src')
              + '&url=' + new URL('//cdn.example.net/p.js', location.href).href
              + '&rooted=' + new URL('/p.js', location.href).href);
            document.body.appendChild(probe);
            </script>
            </body></html>
            """;

        var rendered = await RenderAsync(engine, html);

        Assert.Contains("anchor=https://cdn.example.net/js/loader.js", rendered);
        Assert.Contains("script=https://cdn.example.net/chunk.js", rendered);
        // The attribute still holds what the page wrote — a chunk runtime derives a chunk's identity from it.
        Assert.Contains("attribute=//cdn.example.net/chunk.js", rendered);
        Assert.Contains("url=https://cdn.example.net/p.js", rendered);
        Assert.Contains("rooted=https://example.test/p.js", rendered);
    }

    // The pair is how a page assembles a request. A URL whose components are readonly, and a params list that
    // only understands a query string, both answer without complaining: the tag the page then appends carries
    // the URL it started with, or a query that serialized to nothing.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_AUrlIsLiveAndItsParamsTakeEveryInitShape(JsEngine engine)
    {
        const string html = """
            <html><body>
            <script>
            // The probe carries these in one attribute, so an ampersand inside a value has to survive the
            // serializer's escaping as something else.
            function q(v) { return String(v).split('&').join('|'); }
            var u = new URL('https://origin.test/a?b=1#f');
            u.searchParams.set('id', 'X-1');
            var afterSet = u.href;
            u.pathname = '/z';
            u.hash = 'q';
            var probe = document.createElement('a');
            probe.setAttribute('href', '/probe'
              + '?afterSet=' + q(afterSet)
              + '&afterAssign=' + q(u.href)
              + '&search=' + q(u.search)
              + '&origin=' + u.origin
              + '&fromRecord=' + q(new URLSearchParams({ id: 'X-1', l: 'dataLayer' }))
              + '&fromPairs=' + q(new URLSearchParams([['id', 'X-1'], ['l', 'dl']]))
              + '&fromString=' + new URLSearchParams('?id=X-1')
              + '&decoded=' + new URL('https://origin.test/?q=a%20b').searchParams.get('q'));
            document.body.appendChild(probe);
            </script>
            </body></html>
            """;

        var rendered = await RenderAsync(engine, html);

        Assert.Contains("afterSet=https://origin.test/a?b=1|id=X-1#f", rendered);
        Assert.Contains("afterAssign=https://origin.test/z?b=1|id=X-1#q", rendered);
        Assert.Contains("search=?b=1|id=X-1", rendered);
        Assert.Contains("origin=https://origin.test", rendered);
        Assert.Contains("fromRecord=id=X-1|l=dataLayer", rendered);
        Assert.Contains("fromPairs=id=X-1|l=dl", rendered);
        Assert.Contains("fromString=id=X-1", rendered);
        Assert.Contains("decoded=a b", rendered);
    }

    private async Task<string> RenderAsync(JsEngine engine, string html)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(
            Encoding.UTF8.GetBytes(html), "https://example.test/", new HttpClient(), TestContext.Current.CancellationToken);
        return Encoding.UTF8.GetString(result);
    }
}
