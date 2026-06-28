using AngleSharp.Dom;
using System.Dynamic;
using System.Linq.Expressions;

namespace Crawler.AngleSharp.Js.Dom.Expando;

internal sealed class JsExpandoElement : JsElement, IDynamicMetaObjectProvider, IExpandoNode
{
    internal JsExpandoElement(IElement element, DomContext context) : base(element, context)
    {
    }

    public DynamicMetaObject GetMetaObject(Expression parameter) => new ExpandoMetaObject(parameter, this);

    bool IExpandoNode.HasExpando(string name) => Context.TryGetExpando(Node, name, out _);
    object? IExpandoNode.ExpandoGet(string name) => Context.TryGetExpando(Node, name, out var value) ? value : null;
    void IExpandoNode.ExpandoSet(string name, object? value) => Context.SetExpando(Node, name, value);
    void IExpandoNode.ExpandoDelete(string name) => Context.RemoveExpando(Node, name);
    IEnumerable<string> IExpandoNode.ExpandoNames() => Context.ExpandoNames(Node);
}
