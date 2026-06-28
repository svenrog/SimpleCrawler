using Crawler.AngleSharp.Js.Abstractions;

namespace Crawler.AngleSharp.Js.Dom;

// This property is here so webpack's chunk loader (`script.src=url`) writes the
// attribute the renderer reads back when it fetches and executes the dynamically appended chunk.

public partial class JsElement : JsNode, IJsLocation
{
    public string src
    {
        get => Element.GetAttribute("src") ?? string.Empty;
        set => Element.SetAttribute("src", value ?? string.Empty);
    }
}
