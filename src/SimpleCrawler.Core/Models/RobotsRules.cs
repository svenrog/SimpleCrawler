namespace Crawler.Core.Models;

public readonly struct RobotsRules(bool index, bool follow)
{
    public readonly bool Index = index;
    public readonly bool Follow = follow;

    public static RobotsRules All => new(index: true, follow: true);
    public static RobotsRules None => new(index: false, follow: false);
}
