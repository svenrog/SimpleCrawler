using AngleSharp.Dom;
using System.Dynamic;

namespace Crawler.AngleSharp.Js.Dom;

public class JsNode : DynamicObject
{
    private Dictionary<string, object?>? _expando;

    internal JsNode(INode node, DomContext context)
    {
        Node = node;
        Context = context;
    }

    internal INode Node { get; }
    internal DomContext Context { get; }

    public int nodeType => (int)Node.NodeType;
    public string nodeName => Node.NodeName;

    public object? parentNode => Context.Wrap(Node.Parent);
    public object? firstChild => Context.Wrap(Node.FirstChild);
    public object? lastChild => Context.Wrap(Node.LastChild);
    public object? nextSibling => Context.Wrap(Node.NextSibling);
    public object? previousSibling => Context.Wrap(Node.PreviousSibling);
    public object childNodes => Context.WrapAll(Node.ChildNodes);

    public string textContent
    {
        get => Node.TextContent;
        set => TrySetDomProperty("textContent", value);
    }

    public object? appendChild(JsNode child)
    {
        Node.AppendChild(child.Node);
        return child;
    }

    public object? insertBefore(JsNode child, JsNode? reference)
    {
        Node.InsertBefore(child.Node, reference?.Node);
        return child;
    }

    public object? removeChild(JsNode child)
    {
        Node.RemoveChild(child.Node);
        return child;
    }

    public object? replaceChild(JsNode node, JsNode old)
    {
        Node.ReplaceChild(node.Node, old.Node);
        return old;
    }

    public void remove() => Node.Parent?.RemoveChild(Node);

    public bool contains(JsNode? other)
    {
        for (var node = other?.Node; node != null; node = node.Parent)
        {
            if (ReferenceEquals(node, Node))
                return true;
        }

        return false;
    }

    public void addEventListener(object? type, object? listener = null, object? options = null) { }
    public void removeEventListener(object? type, object? listener = null, object? options = null) { }
    public bool dispatchEvent(object? @event = null) => true;

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        if (_expando is null)
        {
            result = null;
            return false;
        }

        return _expando.TryGetValue(binder.Name, out result);
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        if (TrySetDomProperty(binder.Name, value))
            return true;

        _expando ??= new Dictionary<string, object?>(StringComparer.Ordinal);
        _expando[binder.Name] = value;

        return true;
    }

    public override IEnumerable<string> GetDynamicMemberNames() => _expando?.Keys ?? Enumerable.Empty<string>();

    protected virtual bool TrySetDomProperty(string name, object? value)
    {
        switch (name)
        {
            case "textContent":
            case "nodeValue":
                Node.TextContent = value?.ToString() ?? string.Empty;
                return true;
            default:
                return false;
        }
    }
}
