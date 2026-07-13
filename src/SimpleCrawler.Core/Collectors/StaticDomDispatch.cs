using SimpleCrawler.Core.Models;

namespace SimpleCrawler.Core.Collectors;

/// <summary>
/// <see cref="IDomDispatch"/> for the static backends: every collector reads the same parsed
/// <see cref="IPageDom"/> directly.
/// </summary>
public sealed class StaticDomDispatch : IDomDispatch
{
    private readonly IPageDom _dom;

    public StaticDomDispatch(IPageDom dom)
    {
        _dom = dom;
    }

    public ValueTask Dispatch(UrlReport report, IDomCollector collector, string resolvedUrl)
    {
        if (collector is not IStaticDomCollector staticDomCollector)
            return ValueTask.CompletedTask;

        return staticDomCollector.OnDocument(report, _dom, resolvedUrl);
    }
}
