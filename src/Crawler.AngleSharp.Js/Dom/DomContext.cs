using AngleSharp.Dom;
using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Dom.Expando;
using Crawler.AngleSharp.Js.Dom.Window;

namespace Crawler.AngleSharp.Js.Dom;

public sealed class DomContext
{
    private readonly IJsEngine _engine;
    private readonly bool _enableExpandos;
    private readonly Dictionary<INode, JsNode> _wrappers = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<INode, Dictionary<string, object?>> _expandos = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _tasks = [];

    public DomContext(IDocument document, IJsEngine engine, Uri pageUri, bool enableExpandos = false)
    {
        _engine = engine;
        _enableExpandos = enableExpandos;
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
        Bridge = new DomBridge(this);
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

    public IEnumerable<string> ExpandoNames(INode node) =>
        _expandos.TryGetValue(node, out var bag) ? bag.Keys : [];

    public object CreateArray(IReadOnlyList<object?> items) => _engine.CreateArray(items);

    public void Enqueue(object callback) => _tasks.Add(callback);

    public int PendingTaskCount => _tasks.Count;

    public IReadOnlyList<object> TakeTasks()
    {
        if (_tasks.Count == 0)
            return [];

        var batch = _tasks.ToArray();
        _tasks.Clear();
        return batch;
    }
}
