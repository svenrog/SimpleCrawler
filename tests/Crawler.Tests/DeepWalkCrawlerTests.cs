using Crawler.AngleSharp.Js.Jint;
using Crawler.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Tests;

// Regression for the Jint StackOverflow on bundles that deep-walk the DOM (ce759ea, JintJsEngine only):
// Jint's ObjectWrapper reported every node-wrapper CLR getter (children, parentNode, ownerDocument, ...) as
// an enumerable own key, so a deep clone/merge/serialize walker (Object.keys / spread / JSON.stringify)
// followed the DOM's reference cycles forever and crashed with an uncatchable StackOverflowException
// (www.ewheels.se). The fix reports no enumerable keys for JsNode wrappers, matching a browser's
// Object.keys(divEl) === []. This probe is Jint-only on purpose: V8's host-object enumeration exposes keys
// and never matched that invariant, yet it never overflowed — its deep-walk survival is covered by the real
// SPA crawler tests instead.
[Collection("Crawler")]
public class DeepWalkCrawlerTests : IClassFixture<DeepWalkHostFixture>
{
    private readonly DeepWalkHostFixture _context;

    public DeepWalkCrawlerTests(DeepWalkHostFixture hostFixture)
    {
        _context = hostFixture;
    }

    [Fact]
    public async Task AngleSharpJintCrawler_Walks_Dom_Without_Overflow()
    {
        var subject = _context.ServiceProvider.GetRequiredService<DefaultAngleSharpJintCrawler>();
        var result = await subject.Start(DeepWalkHostFixture.HostName, _context.CancellationSource.Token);

        Assert.NotEmpty(_context.Links);
        Assert.Equal(_context.Links.Count, result.Urls.Count);
        Assert.Equal(_context.Links.ToHashSet(), [.. result.Urls]);
    }
}
