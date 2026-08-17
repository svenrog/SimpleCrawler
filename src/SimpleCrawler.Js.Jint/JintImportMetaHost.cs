using Jint.Native;
using Jint.Runtime;
using SimpleCrawler.Core.Helpers;
using Module = Jint.Runtime.Modules.Module;

namespace SimpleCrawler.Js.Jint;

/// <summary>
/// Answers <c>import.meta</c> with the module's own URL. Jint implements the syntax but delegates the
/// properties to its host, whose default answers none, so <c>import.meta.url</c> reads back
/// <c>undefined</c> — and a loader that does <c>new URL(import.meta.url)</c> to find where its own chunks
/// live throws before it fetches any of them, costing the page every component that entry point defines.
/// The V8 backend never had the gap: ClearScript fills the property from the document it was given.
/// </summary>
internal sealed class JintImportMetaHost : Host
{
    private readonly Uri _baseUri;

    public JintImportMetaHost(Uri baseUri)
    {
        _baseUri = baseUri;
    }

    /// <summary>
    /// The location is the specifier the module was registered under: an absolute URL for a fetched module,
    /// but the raw <c>src</c> path for an entry module and the page URL for an inline one, so it is resolved
    /// against the page before it is handed to script that will parse it as a URL.
    /// </summary>
    public override List<KeyValuePair<JsValue, JsValue>> GetImportMetaProperties(Module moduleRecord)
    {
        var url = UriHelper.GetAbsoluteUrl(_baseUri, moduleRecord.Location) ?? _baseUri.ToString();

        return [new KeyValuePair<JsValue, JsValue>("url", url)];
    }
}
