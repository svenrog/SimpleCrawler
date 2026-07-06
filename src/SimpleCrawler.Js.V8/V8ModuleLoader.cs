using SimpleCrawler.Js.Abstractions;
using Microsoft.ClearScript;
using Microsoft.ClearScript.JavaScript;

namespace SimpleCrawler.Js.V8;

internal sealed class V8ModuleLoader : DocumentLoader
{
    private readonly IModuleFetcher _fetcher;
    private readonly Uri _baseUri;

    // ClearScript invokes the loader for every import; without our own cache the same URL is
    // fetched and compiled as a fresh module each time, so shared singletons (a router context,
    // a framework's options object) end up duplicated and consumers silently read the wrong copy.
    private readonly Dictionary<string, Document> _cache = new(StringComparer.Ordinal);

    public V8ModuleLoader(IModuleFetcher fetcher, Uri baseUri)
    {
        _fetcher = fetcher;
        _baseUri = baseUri;
    }

    // Pre-seed an already-fetched module (the page's entry script) so it resolves to a single
    // cached instance: importing it must not fetch and evaluate a second copy, or shared
    // singletons (a framework's options object, a router context) get duplicated.
    public void Seed(Uri uri, string source)
    {
        _cache[uri.AbsoluteUri] = Build(uri, source);
    }

    public override Document LoadDocument(DocumentSettings settings, DocumentInfo? sourceInfo, string specifier, DocumentCategory category, DocumentContextCallback contextCallback)
    {
        var uri = Resolve(sourceInfo, specifier);
        if (_cache.TryGetValue(uri.AbsoluteUri, out var cached))
            return cached;

        var document = Build(uri, _fetcher.Fetch(uri) ?? "export {};");
        _cache[uri.AbsoluteUri] = document;
        return document;
    }

    private static StringDocument Build(Uri uri, string source)
    {
        return new StringDocument(new DocumentInfo(uri) { Category = ModuleCategory.Standard }, source);
    }

    public override Task<Document> LoadDocumentAsync(DocumentSettings settings, DocumentInfo? sourceInfo, string specifier, DocumentCategory category, DocumentContextCallback contextCallback)
    {
        return Task.FromResult(LoadDocument(settings, sourceInfo, specifier, category, contextCallback));
    }

    private Uri Resolve(DocumentInfo? sourceInfo, string specifier)
    {
        if (Uri.TryCreate(specifier, UriKind.Absolute, out var absolute))
            return absolute;

        var referrer = sourceInfo?.Uri is { IsAbsoluteUri: true } source ? source : _baseUri;
        return new Uri(referrer, specifier);
    }
}
