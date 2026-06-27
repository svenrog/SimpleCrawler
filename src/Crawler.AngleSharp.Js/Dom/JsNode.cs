using AngleSharp.Dom;
using AngleSharp.Html.Dom;

namespace Crawler.AngleSharp.Js.Dom;

public class JsNode
{
    internal JsNode(INode node, DomContext context)
    {
        Node = node;
        Context = context;
    }

    internal INode Node { get; }
    internal DomContext Context { get; }

    public int nodeType => (int)Node.NodeType;
    public string nodeName => Node.NodeName;

    public object? ownerDocument => Context.Wrap(Node.Owner);
    public object? parentNode => Context.Wrap(Node.Parent);
    public object? firstChild => Context.Wrap(Node.FirstChild);
    public object? lastChild => Context.Wrap(Node.LastChild);
    public object? nextSibling => Context.Wrap(Node.NextSibling);
    public object? previousSibling => Context.Wrap(Node.PreviousSibling);
    public object childNodes => Context.WrapAll(Node.ChildNodes);

    public string textContent
    {
        get => Node.TextContent;
        set => Node.TextContent = value ?? string.Empty;
    }

    public string nodeValue
    {
        get => Node.TextContent;
        set => Node.TextContent = value ?? string.Empty;
    }

    public object? appendChild(JsNode child)
    {
        Node.AppendChild(child.Node);
        NotifyIfScript(child.Node);
        return child;
    }

    public object? insertBefore(JsNode child, JsNode? reference)
    {
        Node.InsertBefore(child.Node, reference?.Node);
        NotifyIfScript(child.Node);
        return child;
    }

    private void NotifyIfScript(INode node)
    {
        if (node is IHtmlScriptElement script && !string.IsNullOrEmpty(script.GetAttribute("src")))
            Context.NotifyResourceAppended(script);
        else if (node is IHtmlLinkElement link)
            Context.NotifyResourceAppended(link);
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

    public bool isEqualNode(JsNode? other) => other is not null && Node.Equals(other.Node);
    public bool isSameNode(JsNode? other) => other is not null && ReferenceEquals(Node, other.Node);
    public bool hasChildNodes() => Node.HasChildNodes;
    public object? cloneNode(object? deep = null) => Context.Wrap(Node.Clone(deep is true));

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
}
