using AngleSharp.Dom;
using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Dom.Expando;
using Crawler.AngleSharp.Js.Dom.Window;
using Crawler.AngleSharp.Js.Models;

namespace Crawler.AngleSharp.Js.Dom;

public sealed class DomContext
{
    private readonly IJsEngine _engine;
    private readonly bool _enableExpandos;
    private readonly Dictionary<INode, JsNode> _wrappers = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<INode, Dictionary<string, object?>> _expandos = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _tasks = [];
    private readonly List<IElement> _pendingResources = [];
    private readonly HashSet<INode> _seenResources = new(ReferenceEqualityComparer.Instance);

    public DomContext(IDocument document, IJsEngine engine, Uri pageUri, JsRenderOptions options)
    {
        _engine = engine;
        _enableExpandos = options.EnableDomExpandos;

        Document = document;
        Location = new JsLocation(pageUri);
        History = new JsHistory(Location);
        Navigator = new JsNavigator();
        LocalStorage = new JsLocalStorage();
        SessionStorage = new JsLocalStorage();
        Crypto = new JsCrypto();
        CustomElements = new JsCustomElements();
        Console = new JsConsole();
        Performance = new JsPerformance(this);
        Bridge = new DomBridge(this, options.Viewport);
    }

    public IDocument Document { get; }
    public JsLocation Location { get; }
    public JsHistory History { get; }
    public JsNavigator Navigator { get; }
    public JsLocalStorage LocalStorage { get; }
    public JsLocalStorage SessionStorage { get; }
    public JsCrypto Crypto { get; }
    public JsCustomElements CustomElements { get; }
    public JsConsole Console { get; }
    public JsPerformance Performance { get; }
    public DomBridge Bridge { get; }

    public object? CurrentScript { get; set; }

    // The JS global object, exposed to the bundle as document.defaultView (the `window` it reads
    // through globalThis). Set once globals are wired up; null until then.
    public object? Window { get; set; }

    public JsDocument DocumentWrapper => (JsDocument)Wrap(Document)!;

    public JsNode? Wrap(INode? node)
    {
        if (node is null)
            return null;

        if (_wrappers.TryGetValue(node, out var existing))
            return existing;

        JsNode wrapper = node switch
        {
            IDocument document => _enableExpandos ? new JsExpandoDocument(document, this) : new JsDocument(document, this),
            IElement element => _enableExpandos ? new JsExpandoElement(element, this) : new JsElement(element, this),
            IText text => _enableExpandos ? new JsExpandoText(text, this) : new JsText(text, this),
            _ => _enableExpandos ? new JsExpandoNode(node, this) : new JsNode(node, this)
        };

        _wrappers[node] = wrapper;
        return wrapper;
    }

    public object WrapAll(IEnumerable<INode> nodes)
    {
        if (nodes is IReadOnlyCollection<INode> collection)
        {
            var array = new object?[collection.Count];
            var index = 0;
            foreach (var node in nodes)
                array[index++] = Wrap(node);

            return _engine.CreateArray(array);
        }

        var items = new List<object?>();
        foreach (var node in nodes)
            items.Add(Wrap(node));

        return _engine.CreateArray(items);
    }

    public void SetExpando(INode node, string name, object? value)
    {
        if (!_expandos.TryGetValue(node, out var bag))
            _expandos[node] = bag = [];

        bag[name] = value;
    }

    public bool TryGetExpando(INode node, string name, out object? value)
    {
        if (_expandos.TryGetValue(node, out var bag) && bag.TryGetValue(name, out value))
            return true;

        value = null;
        return false;
    }

    public void RemoveExpando(INode node, string name)
    {
        if (_expandos.TryGetValue(node, out var bag))
            bag.Remove(name);
    }

    public IEnumerable<string> ExpandoNames(INode node) =>
        _expandos.TryGetValue(node, out var bag) ? bag.Keys : [];

    public object CreateArray(IReadOnlyList<object?> items) => _engine.CreateArray(items);

    public void Enqueue(object callback) => _tasks.Add(callback);

    public int PendingTaskCount => _tasks.Count;

    // A <script src> or <link> appended at runtime (webpack lazy-route JS/CSS chunks, React 18's
    // stylesheet loading, the AppInsights/GTM loaders). The renderer drains these between turns: a
    // same-origin <script> is fetched and executed so its module registrations run; a <link>'s load
    // event is fired. Both resolve the import() promise (Promise.all of the JS and CSS chunk) the route
    // awaits — without them a code-split route never loads and the app sits on its loading fallback.
    public void NotifyResourceAppended(IElement resource)
    {
        if (_seenResources.Add(resource))
            _pendingResources.Add(resource);
    }

    public int PendingResourceCount => _pendingResources.Count;

    public IReadOnlyList<IElement> TakePendingResources()
    {
        if (_pendingResources.Count == 0)
            return [];

        var batch = _pendingResources.ToArray();
        _pendingResources.Clear();
        return batch;
    }

    public IReadOnlyList<object> TakeTasks()
    {
        if (_tasks.Count == 0)
            return [];

        var batch = _tasks.ToArray();
        _tasks.Clear();
        return batch;
    }
}
