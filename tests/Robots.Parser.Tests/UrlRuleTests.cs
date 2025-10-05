using Crawler.Core.Robots;
using FluentAssertions;
using Xunit;

namespace Robots.Parser.Tests;

public class UrlRuleTests
{
    [Fact]
    public void Matches_EmptyRulePath_ReturnFalse()
    {
        // Arrange
        var pattern = new UrlPathPattern("");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(false);
    }

    [Fact]
    public void Matches_DifferentPath_ReturnFalse()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/other/path");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(false);
    }

    [Fact]
    public void Matches_DirectoryQualifier_ReturnFalse()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path/");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(false);
    }

    [Fact]
    public void Matches_ExactMatch_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_FileMatch_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path.html");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_SubdirectoryMatch_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path/subdirectory");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterBothLowercase_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path%3c");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path%3c");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterBothUppercase_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path%3C");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path%3C");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterRuleLowercasePathUppercase_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path%3c");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path%3C");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterRuleUppercasePathLowercase_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path%3C");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path%3c");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterForwardSlashBothUrl_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path%2F");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path%2F");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterForwardSlashOnlyInRule_ReturnFalse()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path%2F");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path/");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(false);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterForwardSlashOnlyInPath_ReturnFalse()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path/");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path%2F");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(false);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterAsteriskBothUrl_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some%2Apath");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some%2Apath");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterAsteriskOnlyInRule_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some%2Apath");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some*path");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterAsteriskOnlyInPath_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some*path");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some%2Apath");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterReservedBothUrl_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some%24path");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some%24path");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterReservedOnlyInRule_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some%24path");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some$path");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterReservedOnlyInPath_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some$path");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some%24path");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterNotSpecialLowercaseOnlyInRule_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path%7e");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path~");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterNotSpecialLowercaseOnlyInPath_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path~");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path%7e");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterNotSpecialUppercaseOnlyInRule_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path%7E");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path~");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_PercentEncodedCharacterNotSpecialUppercaseOnlyInPath_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/some/path~");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/some/path%7E");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_UnescapedQueryStringInRuleAndPath_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/foo/bar?baz=https://foo.bar");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/foo/bar?baz=https://foo.bar");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_UnescapedQueryStringInRuleButPathEscaped_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/foo/bar?baz=https://foo.bar");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/foo/bar?baz=https%3A%2F%2Ffoo.bar");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_UnescapedQueryStringInPathButRuleEscaped_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/foo/bar?baz=https%3A%2F%2Ffoo.bar");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/foo/bar?baz=https://foo.bar");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_ExistingUnencodedUtf8Character_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/foo/bar/ツ");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/foo/bar/ツ");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_ExistingEncodedUtf8Character_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/foo/bar/%E3%83%84");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/foo/bar/%E3%83%84");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_ExistingEncodedUtf8CharacterRuleOnly_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/foo/bar/ツ");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/foo/bar/%E3%83%84");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }

    [Fact]
    public void Matches_ExistingEncodedUtf8CharacterPathOnly_ReturnTrue()
    {
        // Arrange
        var pattern = new UrlPathPattern("/foo/bar/%E3%83%84");
        var urlRule = new UrlRule(RuleType.Disallow, pattern);

        // Act
        var path = new UriPath("/foo/bar/ツ");
        var matches = urlRule.Pattern.Matches(path);

        // Assert
        matches.Should().Be(true);
    }
}
