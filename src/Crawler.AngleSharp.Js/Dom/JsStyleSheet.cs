namespace Crawler.AngleSharp.Js.Dom;

public sealed class JsStyleSheet
{
    private readonly DomContext _context;

    internal JsStyleSheet(DomContext context)
    {
        _context = context;
    }

    public object cssRules => _context.CreateArray(Array.Empty<object?>());

    public double insertRule(object? rule, object? index = null) => 0;

    public void deleteRule(object? index) { }
}
