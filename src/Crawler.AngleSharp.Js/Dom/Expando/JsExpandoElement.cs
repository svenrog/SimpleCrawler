using AngleSharp.Dom;
using System.Dynamic;
using System.Linq.Expressions;

namespace Crawler.AngleSharp.Js.Dom.Expando;

internal sealed class JsExpandoElement : JsElement, IDynamicMetaObjectProvider, IExpandoNode
{
    internal JsExpandoElement(IElement element, DomContext context) : base(element, context)
    {
    }

    // Real properties (not free-form expandos) so that `script.onload = fn` lands in the per-node expando
    // table on Jint too: Jint keeps JS-assigned members on its transient object wrapper rather than routing
    // them through the DLR into the table the way ClearScript/V8 does, so a re-wrapped node would lose them.
    // The renderer reads these back from the table when it fires a dynamically loaded chunk's load event,
    // which is what settles webpack's code-split import() promise.
    public object? onload
    {
        get => Context.TryGetExpando(Element, "onload", out var value) ? value : null;
        set => Context.SetExpando(Element, "onload", value);
    }

    public object? onerror
    {
        get => Context.TryGetExpando(Element, "onerror", out var value) ? value : null;
        set => Context.SetExpando(Element, "onerror", value);
    }

    public DynamicMetaObject GetMetaObject(Expression parameter) => new ExpandoMetaObject(parameter, this);

    bool IExpandoNode.HasExpando(string name) => Context.TryGetExpando(Node, name, out _);
    object? IExpandoNode.ExpandoGet(string name) => Context.TryGetExpando(Node, name, out var value) ? value : null;
    void IExpandoNode.ExpandoSet(string name, object? value) => Context.SetExpando(Node, name, value);
    void IExpandoNode.ExpandoDelete(string name) => Context.RemoveExpando(Node, name);
    IEnumerable<string> IExpandoNode.ExpandoNames() => Context.ExpandoNames(Node);
}
