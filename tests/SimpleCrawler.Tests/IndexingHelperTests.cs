using SimpleCrawler.Core.Helpers;
using SimpleCrawler.Core.Models;

namespace SimpleCrawler.Tests;

public class IndexingHelperTests
{
    [Theory]
    [InlineData(null, true, true)]
    [InlineData("", true, true)]
    [InlineData("index", true, true)]
    [InlineData("follow", true, true)]
    [InlineData("noindex", false, true)]
    [InlineData("nofollow", true, false)]
    [InlineData("none", false, false)]
    [InlineData("all", true, true)]
    [InlineData("index, nofollow", true, false)]
    [InlineData("noindex, follow", false, true)]
    [InlineData("  NoIndex  ", false, true)]
    public void ParseMetaRobots_Leaves_Unmentioned_Directive_At_Spec_Default(string? content, bool expectedIndex, bool expectedFollow)
    {
        var rules = IndexingHelper.ParseMetaRobots(content);

        Assert.Equal(expectedIndex, rules.Index);
        Assert.Equal(expectedFollow, rules.Follow);
    }
}
