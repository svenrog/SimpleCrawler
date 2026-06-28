using Crawler.AngleSharp.Js.Abstractions;

namespace Crawler.AngleSharp.Js.Dom;

// onload/onerror are real properties backed by the per-node expando table so `script.onload = fn` survives
// a C# round-trip on Jint (which keeps JS-assigned members on a transient wrapper, not in the table the way
// ClearScript/V8 does). They live on the base JsElement, not the JsExpandoElement subclass: ClearScript's
// FallbackSetMember resolves a real member declared on the base type but throws MissingMemberException for
// one declared on the most-derived dynamic wrapper, which silently broke webpack chunk loading on V8.
public partial class JsElement : JsNode, IJsLocation
{
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
}
