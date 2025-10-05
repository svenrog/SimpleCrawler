// Copyright (c) Adam Shirt (@drmathias). All rights reserved.
// Licensed under MIT. See the LICENSE file in the project root for more information
// https://github.com/drmathias/robots

namespace Crawler.Core.Robots;

/// <summary>
/// Provides the ability to check accessibility of URLs for a robot
/// </summary>
public class RobotRuleChecker : IRobotRuleChecker
{
    public static readonly RobotRuleChecker Empty = new([]);
    private static readonly RuleTypeComparer _ruleComparer = new();

    private readonly HashSet<UrlRule> _rules;

    /// <summary>
    /// Creates a rule checker with a specified set of rules
    /// </summary>
    /// <param name="rules">A set of path rules</param>
    public RobotRuleChecker(HashSet<UrlRule> rules)
    {
        _rules = rules;
    }

    /// <inheritdoc />
    public bool IsAllowed(string path)
    {
        /*
            "The /robots.txt URL is always allowed"
        */
        if (_rules.Count == 0 || path == "/robots.txt")
            return true;

        var uriPath = new UriPath(path);

        var ruleMatch = _rules.Where(rule => rule.Pattern.Matches(uriPath))
                              .OrderByDescending(rule => rule.Pattern.Length)
                              .ThenBy(rule => rule.Type, _ruleComparer)
                              .FirstOrDefault();

        return ruleMatch is null || ruleMatch.Type == RuleType.Allow;
    }
}
