namespace Crawler.Core.Robots;

public sealed class RuleTypeComparer : IComparer<RuleType>
{
    public int Compare(RuleType ruleType, RuleType _) => ruleType switch
    {
        RuleType.Allow => -1,
        RuleType.Disallow => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(ruleType), "Invalid rule type")
    };
}