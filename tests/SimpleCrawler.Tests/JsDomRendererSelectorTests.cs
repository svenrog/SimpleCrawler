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

    private async Task<string> RenderAsync(JsEngine engine)
    {
        if (engine == JsEngine.V8)
            Assert.SkipUnless(V8Support.IsAvailable, V8Support.UnavailableReason);

        var renderer = CreateJsRenderer(engine);
        var result = await renderer.RenderAsync(
            Encoding.UTF8.GetBytes(_html), "https://example.test/", new HttpClient(), TestContext.Current.CancellationToken);
        return Encoding.UTF8.GetString(result);
    }
}
