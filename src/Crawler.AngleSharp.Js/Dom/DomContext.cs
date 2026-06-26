using AngleSharp.Dom;
using Crawler.AngleSharp.Js.Abstractions;

namespace Crawler.AngleSharp.Js.Dom;

public sealed class DomContext
{
    private readonly IJsEngine _engine;
    private readonly Dictionary<INode, JsNode> _wrappers = new(ReferenceEqualityComparer.Instance);
    private readonly List<object> _tasks = [];

    public DomContext(IDocument document, IJsEngine engine, Uri pageUri)
    {
        _engine = engine;
        Document = document;
        Location = new JsLocation(pageUri);
        History = new JsHistory(Location);
        Bridge = new DomBridge(this);
    }

    public IDocument Document { get; }
    public JsLocation Location { get; }
    public JsHistory History { get; }
    public DomBridge Bridge { get; }

    public JsDocument DocumentWrapper => (JsDocument)Wrap(Document)!;

    public JsNode? Wrap(INode? node)
    {
        if (node is null)
            return null;

        if (_wrappers.TryGetValue(node, out var existing))
            return existing;

        JsNode wrapper = node switch
        {
            IDocument document => new JsDocument(document, this),
            IElement element => new JsElement(element, this),
            IText text => new JsText(text, this),
            _ => new JsNode(node, this)
        };

        _wrappers[node] = wrapper;
        return wrapper;
    }

    public object WrapAll(IEnumerable<INode> nodes)
    {
        var items = new List<object?>();
        foreach (var node in nodes)
            items.Add(Wrap(node));

        return _engine.CreateArray(items);
    }

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
