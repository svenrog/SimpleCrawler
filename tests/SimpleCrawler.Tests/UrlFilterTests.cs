using SimpleCrawler.Core.Filtering;

namespace SimpleCrawler.Tests;

/// <summary>
/// Covers include/exclude URL filtering, which reuses the robots.txt glob and longest-match resolution:
/// excludes deny, includes default-deny everything else, and specificity breaks include/exclude ties.
/// </summary>
public class UrlFilterTests
{
    [Fact]
    public void Create_Returns_Null_When_No_Patterns()
    {
        Assert.Null(UrlFilter.Create([], []));
    }

    [Fact]
    public void Exclude_Denies_Match_And_Allows_The_Rest()
    {
        var filter = UrlFilter.Create([], ["/private"])!;

        Assert.False(filter.IsAllowed("/private/page"));
        Assert.True(filter.IsAllowed("/public/page"));
    }

    [Fact]
    public void Include_Denies_Everything_Not_Matched()
    {
        var filter = UrlFilter.Create(["/blog/*"], [])!;

        Assert.True(filter.IsAllowed("/blog/post"));
        Assert.False(filter.IsAllowed("/about"));
    }

    [Fact]
    public void Exclude_Out_Matches_Shorter_Include()
    {
        var filter = UrlFilter.Create(["/blog/*"], ["/blog/private"])!;

        Assert.True(filter.IsAllowed("/blog/post"));
        Assert.False(filter.IsAllowed("/blog/private"));
    }

    [Fact]
    public void Longer_Include_Out_Matches_Exclude()
    {
        var filter = UrlFilter.Create(["/blog/private/*"], ["/blog/*"])!;

        Assert.True(filter.IsAllowed("/blog/private/secret"));
        Assert.False(filter.IsAllowed("/blog/public"));
    }

    [Fact]
    public void End_Anchor_Matches_Exact_Path_Only()
    {
        var filter = UrlFilter.Create([], ["/exact$"])!;

        Assert.False(filter.IsAllowed("/exact"));
        Assert.True(filter.IsAllowed("/exact/more"));
    }

    [Fact]
    public void Wildcard_Spans_A_Path_Segment()
    {
        var filter = UrlFilter.Create(["/a/*/c"], [])!;

        Assert.True(filter.IsAllowed("/a/b/c"));
        Assert.False(filter.IsAllowed("/a/b/d"));
    }

    [Fact]
    public void Pattern_Matches_Against_Query_String()
    {
        var filter = UrlFilter.Create([], ["/search?*"])!;

        Assert.False(filter.IsAllowed("/search?q=1"));
        Assert.True(filter.IsAllowed("/results"));
    }
}
