using SimpleCrawler.Js.Abstractions;

namespace SimpleCrawler.Js.Rendering;

/// <summary>
/// What one render pass carries from step to step: the engine it runs on, the policy every crossing into
/// that engine goes through, the page's two addresses, and the client and token its fetches use. One page,
/// one instance — nothing here outlives the render, unlike the caches on <see cref="JsRenderer"/> itself.
/// <para>
/// It is built after the document is parsed, because the document is what decides
/// <see cref="DocumentBaseUri"/>: everything before that point is engine setup with nothing to carry.
/// </para>
/// </summary>
internal sealed class RenderingContext
{
    public required IJsEngine Engine { get; init; }

    public required RenderIsolation Isolation { get; init; }

    /// <summary>
    /// The page address as the caller wrote it. This is the string an inline script and an inline module
    /// borrow as their own source identity, so it stays the caller's text rather than a round-tripped
    /// <see cref="Uri"/>.
    /// </summary>
    public required string PageUrl { get; init; }

    /// <summary>
    /// The same address parsed, for the comparisons a string cannot make — notably the same-origin test that
    /// decides whether an appended script is executed.
    /// </summary>
    public required Uri PageUri { get; init; }

    /// <summary>
    /// What a relative script or resource URL resolves against: the first <c>&lt;base href&gt;</c> in the
    /// parsed document, else <see cref="PageUri"/>. See <see cref="JsRenderer.ResolveDocumentBase"/>.
    /// </summary>
    public required Uri DocumentBaseUri { get; init; }

    public required HttpClient Client { get; init; }

    public required CancellationToken CancellationToken { get; init; }
}
