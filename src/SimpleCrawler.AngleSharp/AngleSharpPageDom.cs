using AngleSharp.Dom;
using SimpleCrawler.Core.Collectors;

namespace SimpleCrawler.AngleSharp;

/// <summary><see cref="IPageDom"/> over a parsed AngleSharp <see cref="IDocument"/>.</summary>
internal sealed class AngleSharpPageDom : IPageDom
{
    private readonly IDocument _document;

    public AngleSharpPageDom(IDocument document)
    {
        _document = document;
    }

    public IReadOnlyList<IDomElement> QueryAll(string localName)
    {
        var elements = _document.QuerySelectorAll(localName);
        var result = new List<IDomElement>(elements.Length);
        foreach (var element in elements)
            result.Add(new AngleSharpDomElement(element));

        return result;
    }
}
