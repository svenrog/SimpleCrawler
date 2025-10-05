using Crawler.Core.Models;

namespace Crawler.Core.Helpers;

public static class IndexingHelper
{
    public static RobotsRules ParseMetaRobots(string? contentValue)
    {
        if (contentValue == null)
            return RobotsRules.All;

        var contentRules = contentValue.ToLower().Split(", ");
        var index = false;
        var follow = false;

        foreach (var contentRule in contentRules)
        {
            switch (contentRule)
            {
                case "index": index = true; break;
                case "follow": follow = true; break;
                case "noindex": index = false; break;
                case "nofollow": follow = false; break;
                case "all": index = true; follow = true; break;
                case "none": index = false; follow = false; break;
                default: break;
            }
        }

        return new RobotsRules(index, follow);
    }

}
