using SimpleCrawler.Core.Helpers;

namespace SimpleCrawler.Tests;

/// <summary>
/// Covers URL canonicalization used for de-duplication: fragment drop, scheme/host lowercasing,
/// default-port removal, and trailing-slash collapse, all while leaving the query string untouched.
/// </summary>
public class UriNormalizationTests
{
    [Theory]
    [InlineData("http://h/a#frag", "http://h/a")]
    [InlineData("http://h/a?x=1#frag", "http://h/a?x=1")]
    [InlineData("http://h:80/a", "http://h/a")]
    [InlineData("https://h:443/a", "https://h/a")]
    [InlineData("http://h:8080/a", "http://h:8080/a")]
    [InlineData("HTTP://H.COM/A", "http://h.com/A")]
    [InlineData("http://h/a/", "http://h/a")]
    [InlineData("http://h/a/?x=1", "http://h/a?x=1")]
    [InlineData("http://h/", "http://h/")]
    [InlineData("http://h", "http://h/")]
    [InlineData("http://h/a?b=2&a=1", "http://h/a?b=2&a=1")]
    public void Normalize_Canonicalizes(string input, string expected)
    {
        Assert.Equal(expected, UriHelper.Normalize(input));
    }

    [Fact]
    public void Normalize_Leaves_Non_Absolute_Input_Unchanged()
    {
        Assert.Equal("/relative/path", UriHelper.Normalize("/relative/path"));
    }
}
