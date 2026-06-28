namespace Crawler.Tests.Fixtures;

// Each value is a JS-engine capability that a real bundle depends on, encoded as a minimal shell that only
// renders its links when the capability works. The order maps to the per-capability host port.
public enum ProbeCapability
{
    AnchorHref,
    Expando,
    Fetch,
    DeferredCallback,
    JQuery,
    BrowserApis,
    DeepWalk,
}
