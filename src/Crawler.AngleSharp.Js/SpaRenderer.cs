using System.Buffers;
using System.Text;
using System.Text.Json;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;
using JavaScriptEngineSwitcher.Core;
using Microsoft.Extensions.Logging;

namespace Crawler.AngleSharp.Js;

public sealed class SpaRenderer
{
    private static readonly HtmlParser _parser = new();
    private static readonly string _shim = LoadShim();

    private readonly IJsEngineSwitcher _switcher;
    private readonly JsRenderOptions _options;
    private readonly ILogger _logger;

    public SpaRenderer(IJsEngineSwitcher switcher, JsRenderOptions options, ILogger logger)
    {
        _switcher = switcher;
        _options = options;
        _logger = logger;
    }

    public async Task<byte[]> RenderAsync(byte[] shell, string pageUrl, HttpClient client, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream(shell, writable: false);
        using var document = await _parser.ParseDocumentAsync(stream, cancellationToken);

        var hydrateJson = BuildHydrateJson(document.DocumentElement);
        var (classicScripts, moduleEntries) = await CollectScriptsAsync(document, pageUrl, client, cancellationToken);
        var moduleScript = await EsModuleLoader.BuildAsync(moduleEntries, client, cancellationToken);

        var rendered = RunEngine(pageUrl, hydrateJson, classicScripts, moduleScript);
        return Encoding.UTF8.GetBytes(rendered);
    }

    private string RunEngine(string pageUrl, string hydrateJson, IReadOnlyList<string> classicScripts, string? moduleScript)
    {
        using var engine = _switcher.CreateDefaultEngine();

        engine.Execute(_shim);
        engine.Execute($"__crawler.setLocation({JsonSerializer.Serialize(pageUrl)});");
        engine.Execute($"__crawler.hydrate({hydrateJson});");

        foreach (var script in classicScripts)
            Run(engine, script, pageUrl);

        if (moduleScript != null)
            Run(engine, moduleScript, pageUrl);

        // pump() runs one batch of our timer queue per call; the boundary between Evaluate
        // calls lets the engine flush native promise jobs (Preact's lazy resolution and
        // rerenders) so the DOM is fully settled before we serialize it.
        var iterations = 0;
        while (engine.Evaluate<int>("__crawler.pump()") > 0 && iterations++ < _options.MaxTaskDrainIterations)
        {
        }

        return engine.Evaluate<string>("__crawler.serialize()");
    }

    private void Run(IJsEngine engine, string script, string pageUrl)
    {
        try
        {
            engine.Execute(script);
        }
        catch (JsException ex)
        {
            _logger.LogWarning("Bundle execution error on '{url}': {message}", pageUrl, ex.Message);
        }
    }

    private static async Task<(IReadOnlyList<string> Classic, IReadOnlyList<ModuleSource> Modules)> CollectScriptsAsync(IDocument document, string pageUrl, HttpClient client, CancellationToken cancellationToken)
    {
        var baseUri = new Uri(pageUrl);
        var classic = new List<string>();
        var modules = new List<ModuleSource>();

        foreach (var element in document.QuerySelectorAll("script"))
        {
            var script = (IHtmlScriptElement)element;
            var type = script.Type;
            if (!string.IsNullOrEmpty(type) && type is not "text/javascript" and not "module" and not "application/javascript")
                continue;

            var isModule = string.Equals(type, "module", StringComparison.Ordinal);
            var src = script.GetAttribute("src");

            if (string.IsNullOrEmpty(src))
            {
                if (string.IsNullOrEmpty(script.TextContent))
                    continue;

                if (isModule)
                    modules.Add(new ModuleSource(pageUrl, script.TextContent, baseUri));
                else
                    classic.Add(script.TextContent);

                continue;
            }

            var absolute = new Uri(baseUri, src);
            using var response = await client.GetAsync(absolute, cancellationToken);
            if (!response.IsSuccessStatusCode)
                continue;

            var source = await response.Content.ReadAsStringAsync(cancellationToken);

            if (isModule)
                modules.Add(new ModuleSource(absolute.ToString(), source, absolute));
            else
                classic.Add(source);
        }

        return (classic, modules);
    }

    private static string BuildHydrateJson(IElement? root)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            if (root == null)
                writer.WriteNullValue();
            else
                WriteNode(writer, root);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WriteNode(Utf8JsonWriter writer, INode node)
    {
        if (node.NodeType == NodeType.Text)
        {
            writer.WriteStartObject();
            writer.WriteString("text", node.TextContent);
            writer.WriteEndObject();
            return;
        }

        if (node is not IElement element)
            return;

        writer.WriteStartObject();
        writer.WriteString("tag", element.LocalName);

        if (element.Attributes.Length > 0)
        {
            writer.WriteStartObject("attrs");
            foreach (var attribute in element.Attributes)
                writer.WriteString(attribute.Name, attribute.Value);
            writer.WriteEndObject();
        }

        writer.WriteStartArray("children");
        foreach (var child in element.ChildNodes)
        {
            if (child is IElement childElement && childElement.LocalName == "script")
                continue;

            if (child.NodeType is NodeType.Text or NodeType.Element)
                WriteNode(writer, child);
        }
        writer.WriteEndArray();

        writer.WriteEndObject();
    }

    private static string LoadShim()
    {
        var assembly = typeof(SpaRenderer).Assembly;
        var name = Array.Find(assembly.GetManifestResourceNames(), n => n.EndsWith("dom-shim.js", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Embedded dom-shim.js resource was not found.");

        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
