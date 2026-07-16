using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SimpleCrawler.Js.Models;
using SimpleCrawler.Js.Rendering;
using SimpleCrawler.Tests.Fixtures;
using SimpleCrawler.Tests.Models;

namespace SimpleCrawler.Tests;

/// <summary>
/// Shared renderer setup for the JsDomRenderer*Tests classes, which together exercise the pure-JS DOM path
/// (dom.js) without a host: dom.js parses the shell, the inline script mutates the JS DOM, and the tree is
/// serialized back to HTML — no managed DOM wrappers involved. Theory'd over both engines since the JS DOM is
/// the single code path for Jint + V8.
/// </summary>
public abstract class JsDomRendererTestBase
{
    private readonly JsRendererFixture _fixture;

    protected JsDomRendererTestBase(JsRendererFixture fixture)
    {
        _fixture = fixture;
    }

    protected JsRenderer CreateJsRenderer(JsEngine engine, JsRenderOptions? options = null, ILogger? logger = null)
    {
        var factory = _fixture.GetFactory(engine);
        return new JsRenderer(factory, options ?? new JsRenderOptions(), logger ?? NullLogger.Instance);
    }
}
