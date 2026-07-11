using SimpleCrawler.Core.Robots;

namespace SimpleCrawler.Core.Filtering;

/// <summary>
/// Decides whether a discovered URL path is in scope, using the same robots.txt matching primitives
/// (<see cref="UrlPathPattern"/> globs and longest-match, allow-wins resolution) rather than robots
/// semantics themselves: no <c>/robots.txt</c> exemption and no empty-set default.
///
/// Excludes become disallow rules. Includes become allow rules plus an implicit disallow of everything,
/// which loses to any matching include under longest-match, so a URL matching no include is denied.
/// </summary>
public sealed class UrlFilter
{
    private readonly UrlRule[] _rules;

    private UrlFilter(UrlRule[] rules)
    {
        _rules = rules;
    }

    /// <summary>
    /// Builds a filter from include/exclude path patterns, or returns null when neither is supplied so the
    /// crawler can skip filtering entirely.
    /// </summary>
    public static UrlFilter? Create(IReadOnlyList<string> includes, IReadOnlyList<string> excludes)
    {
        if (includes.Count == 0 && excludes.Count == 0)
            return null;

        var rules = new List<UrlRule>(includes.Count + excludes.Count + 1);

        if (includes.Count > 0)
        {
            rules.Add(new UrlRule(RuleType.Disallow, new UrlPathPattern("/")));
            foreach (var pattern in includes)
                rules.Add(new UrlRule(RuleType.Allow, new UrlPathPattern(pattern)));
        }

        foreach (var pattern in excludes)
            rules.Add(new UrlRule(RuleType.Disallow, new UrlPathPattern(pattern)));

        return new UrlFilter([.. rules]);
    }

    /// <summary>
    /// True when the path-and-query is allowed by the configured includes/excludes. The most specific rule
    /// wins; an allow and a disallow of equal specificity resolve to allow.
    /// </summary>
    public bool IsAllowed(string pathAndQuery)
    {
        var path = new UriPath(pathAndQuery);

        UrlRule? match = null;
        foreach (var rule in _rules)
        {
            if (!rule.Pattern.Matches(path))
                continue;

            if (match is null
                || rule.Pattern.Length > match.Pattern.Length
                || (rule.Pattern.Length == match.Pattern.Length && rule.Type == RuleType.Allow))
            {
                match = rule;
            }
        }

        return match is null || match.Type == RuleType.Allow;
    }
}
