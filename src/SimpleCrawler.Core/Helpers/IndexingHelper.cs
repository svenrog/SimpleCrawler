using Crawler.Core.Models;

namespace Crawler.Core.Helpers;

public static class IndexingHelper
{
    public static RobotsRules ParseMetaRobots(string? contentValue)
    {
        if (contentValue == null)
            return RobotsRules.All;

        var index = false;
        var follow = false;

        var span = contentValue.AsSpan();
        foreach (var range in span.SplitAny(", \t"))
        {
            var token = span[range].Trim();
            if (token.IsEmpty)
                continue;

            var (ruleIndex, ruleFollow) = ParseRule(token);
            index = ruleIndex ?? index;
            follow = ruleFollow ?? follow;
        }

        return new RobotsRules(index, follow);
    }

    private static (bool? Index, bool? Follow) ParseRule(ReadOnlySpan<char> rule)
    {
        if (rule.Length > 8)
            return (null, null);

        Span<char> normalized = stackalloc char[rule.Length];
        rule.ToLowerInvariant(normalized);

        return normalized switch
        {
            "index" => (true, null),
            "follow" => (null, true),
            "noindex" => (false, null),
            "nofollow" => (null, false),
            "all" => (true, true),
            "none" => (false, false),
            _ => (null, null),
        };
    }
}
