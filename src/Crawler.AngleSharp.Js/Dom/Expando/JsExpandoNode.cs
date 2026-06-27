using AngleSharp.Dom;
using System.Dynamic;
using System.Linq.Expressions;

namespace Crawler.AngleSharp.Js.Dom.Expando;

// Expando-capable JsNode used only when EnableDomExpandos is on. Inherits the full DOM surface and adds
// JS-expando storage via ExpandoMetaObject; the IExpandoNode hooks are explicit so they stay invisible to JS.
internal sealed class JsExpandoNode : JsNode, IDynamicMetaObjectProvider, IExpandoNode
{
    internal JsExpandoNode(INode node, DomContext context) : base(node, context)
    {
    }

    public DynamicMetaObject GetMetaObject(Expression parameter) => new ExpandoMetaObject(parameter, this);

    bool IExpandoNode.HasExpando(string name) => Context.TryGetExpando(Node, name, out _);
    object? IExpandoNode.ExpandoGet(string name) => Context.TryGetExpando(Node, name, out var value) ? value : null;
    void IExpandoNode.ExpandoSet(string name, object? value) => Context.SetExpando(Node, name, value);
    void IExpandoNode.ExpandoDelete(string name) => Context.RemoveExpando(Node, name);
    IEnumerable<string> IExpandoNode.ExpandoNames() => Context.ExpandoNames(Node);
}
