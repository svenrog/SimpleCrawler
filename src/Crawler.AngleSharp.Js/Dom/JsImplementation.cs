using AngleSharp.Dom;

namespace Crawler.AngleSharp.Js.Dom;

// jQuery's support detection probes document.implementation.createHTMLDocument("") during init; without it
// the IIFE throws before assigning window.jQuery and every later bundle reading the jQuery global fails.
public sealed class JsImplementation
{
    private readonly IDocument _document;
    private readonly DomContext _context;

    internal JsImplementation(IDocument document, DomContext context)
    {
        _document = document;
        _context = context;
    }

    public object createHTMLDocument(object? title = null) =>
        _context.Wrap(_document.Implementation.CreateHtmlDocument(title?.ToString() ?? string.Empty))!;
}
