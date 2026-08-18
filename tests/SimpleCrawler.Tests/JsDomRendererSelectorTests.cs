using SimpleCrawler.Js.Models;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Helpers;
using SimpleCrawler.Tests.Models;
using System.Text;

namespace SimpleCrawler.Tests;

/// <summary>
/// The selector engine behind querySelector/querySelectorAll/matches/closest. Combinators and structural
/// pseudo-classes are the load-bearing part: a page reads <c>container.querySelector('.x &gt; :first-child')
/// .textContent</c> without guarding it, so a selector that answers the wrong element — or null where a
/// browser finds one — throws inside the page's own init and costs everything after it.
/// </summary>
[Collection("Crawler")]
public class JsDomRendererSelectorTests : JsDomRendererTestBase, IClassFixture<JsRendererFixture>
{
    public JsDomRendererSelectorTests(JsRendererFixture fixture) : base(fixture)
    {
    }

    // The tree every case below queries: two sibling containers holding the same classes, so a selector that
    // ignores its ancestor clause answers with more than it should.
    private const string _html = """
        <!doctype html><html><body>
        <div class="wrap"><p class="a">1</p><span class="a">2</span><p class="a b">3</p><em>4</em></div>
        <div class="other"><p class="a">outside</p></div>
        <ul id="l"><li>x</li><li class="sel">y</li><li>z</li></ul>
        <script>
        function count(sel, root) { return (root || document).querySelectorAll(sel).length; }
        var wrap = document.querySelector('.wrap');
        var em = document.querySelector('em');
        var probe = document.createElement('a');
        probe.setAttribute('href', '/probe'
          + '?child=' + count('.wrap > p.a')
          + '&descendant=' + count('.wrap p.a')
          + '&bare=' + count('p.a')
          + '&adjacent=' + count('p.a + span')
          + '&sibling=' + count('p.a ~ em')
          + '&firstChild=' + count('.wrap > :first-child')
          + '&lastOfType=' + count('#l li:last-of-type')
          + '&nth=' + count('#l li:nth-child(2)')
          + '&not=' + count('#l li:not(.sel)')
          + '&has=' + count('div:has(> em)')
          + '&self=' + count('.wrap', wrap)
          + '&scoped=' + count('p.a', wrap)
          + '&matches=' + em.matches('.wrap > em') + '/' + em.matches('.other > em')
          + '&closest=' + em.closest('div').className);
        document.body.appendChild(probe);
        </script>
        </body></html>
        """;

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_CombinatorsConstrainWhatASelectorAnswersWith(JsEngine engine)
    {
        var rendered = await RenderAsync(engine);

        Assert.Contains("child=2", rendered);
        Assert.Contains("descendant=2", rendered);
        // No ancestor clause, so the third .a in the other container counts too.
        Assert.Contains("bare=3", rendered);
        Assert.Contains("adjacent=1", rendered);
        Assert.Contains("sibling=1", rendered);
        Assert.Contains("matches=true/false", rendered);
        Assert.Contains("closest=wrap", rendered);
    }

    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_StructuralPseudoClassesSelectByPosition(JsEngine engine)
    {
        var rendered = await RenderAsync(engine);

        Assert.Contains("firstChild=1", rendered);
        Assert.Contains("lastOfType=1", rendered);
        Assert.Contains("nth=1", rendered);
        Assert.Contains("not=2", rendered);
        Assert.Contains("has=1", rendered);
    }

    // A query is over an element's descendants: the root is the :scope it resolves against, never a candidate.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_QuerySelectorAll_NeverAnswersWithItsOwnRoot(JsEngine engine)
    {
        var rendered = await RenderAsync(engine);

        Assert.Contains("self=0", rendered);
        Assert.Contains("scoped=2", rendered);
    }

    // What a selector engine answers to garbage is a feature test, not an edge case: jQuery decides whether to
    // use querySelectorAll at all by handing it "*,:x" and watching for the throw, and reads an empty list as
    // "this engine is broken" — after which every selector carrying a comma and a colon goes to its own
    // matcher, which then rejects the plain CSS this one supports. The two halves have to hold together: a
    // name outside CSS is a SyntaxError, and a pseudo-class that is real CSS but unsatisfiable here answers
    // with nothing.
    [Theory]
    [InlineData(JsEngine.Jint)]
    [InlineData(JsEngine.V8)]
    public async Task JsMode_AnInvalidSelectorThrowsAndAnUnsatisfiableOneMatchesNothing(JsEngine engine)
    {
        const string html = """
            <!doctype html><html><body>
            <div class="wrap md:flex"><p class="a">1</p><details open><summary>s</summary></details></div>
            <script>
            function verdict(sel) {
              try { return String(document.querySelectorAll(sel).length); } catch (e) { return e.name; }
            }
            var probe = document.createElement('a');
            probe.setAttribute('href', '/probe'
              + '?postComma=' + verdict('*,:x')
              + '&unknownPseudo=' + verdict(':x')
              + '&libraryPseudo=' + verdict(':contains(x)')
              + '&notAnIdentifier=' + verdict('<<')
              + '&escapedNewline=' + verdict('\\\f')
              + '&hover=' + verdict(':hover')
              + '&pseudoElement=' + verdict('p::before')
              + '&vendor=' + verdict(':-moz-focusring')
              + '&open=' + verdict('details:open')
              + '&escapedColon=' + verdict('.md\\:flex')
              + '&plain=' + verdict('.wrap > p.a')
              + '&matches=' + (function () {
                  try { return String(document.body.matches(':visible')); } catch (e) { return e.name; }
                })());
            document.body.appendChild(probe);
            </script>
            </body></html>
            """;

        var rendered = await RenderAsync(engine, html);

        Assert.Contains("postComma=SyntaxError", rendered);
        Assert.Contains("unknownPseudo=SyntaxError", rendered);
        Assert.Contains("libraryPseudo=SyntaxError", rendered);
        Assert.Contains("notAnIdentifier=SyntaxError", rendered);
        Assert.Contains("escapedNewline=SyntaxError", rendered);
        // Element.matches is the other half of the same probe, and jQuery blacklists it the same way.
        Assert.Contains("matches=SyntaxError", rendered);
        Assert.Contains("hover=0", rendered);
        Assert.Contains("pseudoElement=0", rendered);
        Assert.Contains("vendor=0", rendered);
        Assert.Contains("open=1", rendered);
        Assert.Contains("escapedColon=1", rendered);
        Assert.Contains("plain=1", rendered);
    }

    private Task<string> RenderAsync(JsEngine engine) => RenderAsync(engine, _html);

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
