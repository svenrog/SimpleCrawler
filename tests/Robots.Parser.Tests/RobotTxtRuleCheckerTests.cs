using Crawler.Core.Robots;
using FluentAssertions;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Robots.Txt.Parser.Tests.Unit;

public partial class RobotsTxtParserTests
{
    [Fact]
    public async Task NoMatchedUserAgent_AnyPath_Allow()
    {
        // Arrange
        var file =
@"User-agent: AnotherBot
Disallow: /
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);

        ruleChecker!.IsAllowed("/some/path").Should().Be(true);
    }

    [Fact]
    public async Task UserAgentWildcard_DisallowAll_RobotsTxtAllowed()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /

User-agent: AnotherBot
Disallow: /
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/robots.txt").Should().Be(true);
    }

    [Fact]
    public async Task UserAgentWildcard_DisallowPath_DisallowOnMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /some/path

User-agent: AnotherBot
Disallow: /
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(false);
    }

    [Fact]
    public async Task UserAgentWildcard_DisallowWildcardPath_DisallowOnMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /some/*/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/other/sub/path").Should().Be(false);
    }

    [Fact]
    public async Task UserAgentWildcard_DisallowDoubleWildcardPath_DisallowOnMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /some/**/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/other/sub/path").Should().Be(false);
    }

    [Fact]
    public async Task UserAgentWildcard_TwoPartWildcardPath_DisallowOnMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /some/*/*/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/other/sub/path").Should().Be(false);
    }

    [Fact]
    public async Task UserAgentWildcard_TwoPartWildcardPath_DisallowSubpathMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /some/*/*/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/other/sub/path/end").Should().Be(false);
    }

    [Fact]
    public async Task UserAgentWildcard_WildcardPathWithEndOfMatch_AllowSubpathMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /some/*/*/path$
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/other/sub/path/end").Should().Be(true);
    }

    [Fact]
    public async Task UserAgentWildcard_DisallowEndOfMatchPath_DisallowOnExactMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /some/path$
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(false);
    }

    [Fact]
    public async Task UserAgentWildcard_DisallowEndOfMatchPath_AllowOnSubPathMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /some/path$
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path/subdirectory").Should().Be(true);
    }

    [Fact]
    public async Task UserAgentWildcard_DisallowPath_DisallowOnSubpath()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /some/path

User-agent: AnotherBot
Disallow:
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path/subdirectory").Should().Be(false);
    }

    [Fact]
    public async Task UserAgentWildcard_DisallowPath_AllowWhenDoesNotMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /some/path

User-agent: AnotherBot
Disallow:
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/other/path").Should().Be(true);
    }

    [Fact]
    public async Task MatchedUserAgent_NoDisallowRule_AllowAnyPath()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /some/path

User-agent: SomeBot
Disallow: 
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(true);
    }

    [Fact]
    public async Task MatchedUserAgent_DisallowAll_RobotsTxtAllowed()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow:

User-agent: SomeBot
Disallow: /
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/robots.txt").Should().Be(true);
    }

    [Fact]
    public async Task MatchedUserAgent_DisallowPath_DisallowOnPathMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: 

User-agent: SomeBot
Disallow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(false);
    }

    [Fact]
    public async Task MatchedUserAgent_DisallowPath_DisallowOnSubpathMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: 

User-agent: SomeBot
Disallow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path/subdirectory").Should().Be(false);
    }

    [Fact]
    public async Task UserAgentMatch_DisallowPath_AllowWhenPathDoesNotMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: 

User-agent: SomeBot
Disallow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/other/path").Should().Be(true);
    }

    [Fact]
    public async Task MultiLineUserAgentMatch_NoDisallowRule_AllowAnyPath()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /some/path

User-agent: SomeBot
User-agent: SomeOtherBot
Disallow: 
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(true);
    }

    [Fact]
    public async Task MultiLineUserAgentMatch_DisallowPath_DisallowOnPathMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: 

User-agent: SomeBot
User-agent: SomeOtherBot
Disallow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(false);
    }

    [Fact]
    public async Task MultiLineUserAgentMatch_DisallowPath_DisallowOnSubpathMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: 

User-agent: SomeBot
User-agent: SomeOtherBot
Disallow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path/subdirectory").Should().Be(false);
    }

    [Fact]
    public async Task MultiLineUserAgentMatch_DisallowPath_AllowWhenPathDoesNotMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: 

User-agent: SomeBot
User-agent: SomeOtherBot
Disallow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/other/path").Should().Be(true);
    }

    [Fact]
    public async Task MultiGroupUserAgentMatch_DisallowPaths_AllRulesRespected()
    {
        // Arrange
        var file =
@"User-agent: SomeBot
Disallow: /some/path

User-agent: SomeBot
Disallow: /some/other/path

User-agent: SomeBot
Disallow: /yet/another/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(false);
        ruleChecker!.IsAllowed("/some/other/path").Should().Be(false);
        ruleChecker!.IsAllowed("/yet/another/path").Should().Be(false);
    }

    [Fact]
    public async Task WildcardUserAgent_DisallowAllAndNoAllowPath_DisallowAll()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /

User-agent: AnotherBot
Allow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(false);
    }

    [Fact]
    public async Task WildcardUserAgent_DisallowAllAndAllowPath_DisallowIfNotAllowPath()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /
Allow: /some/other/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(false);
    }

    [Fact]
    public async Task WildcardUserAgent_DisallowAllAndAllowPath_AllowPathMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /
Allow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(true);
    }

    [Fact]
    public async Task UserAgentWildcard_DisallowAllAndAllowWildcardPath_AllowWildcardPathMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /
Allow: /some/*/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/other/sub/path").Should().Be(true);
    }

    [Fact]
    public async Task WildcardUserAgentRuleMatch_DisallowAllAndAllowPath_AllowSubpathMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /
Allow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path/subdirectory").Should().Be(true);
    }

    [Fact]
    public async Task WildcardUserAgent_BothAllowAndDisallowSamePath_PreferAllowRule()
    {
        // Arrange
        var file =
@"User-agent: *
Allow: /some/path
Disallow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(true);
    }

    [Fact]
    public async Task WildcardUserAgent_DisallowPathWrongCase_Allow()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /SoMe/paTh
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(true);
    }

    [Fact]
    public async Task MatchedUserAgent_DisallowAllAndAllowPath_DisallowIfNotAllowPath()
    {
        // Arrange
        var file =
@"User-agent: *
Allow: /

User-agent: SomeBot
Disallow: /
Allow: /some/other/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(false);
    }

    [Fact]
    public async Task MatchedUserAgent_DisallowAllAndAllowPath_AllowPathMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /

User-agent: SomeBot
Disallow: /
Allow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(true);
    }

    [Fact]
    public async Task MatchedUserAgent_DisallowAllAndAllowPath_AllowSubpathMatch()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /

User-agent: SomeBot
Disallow: /
Allow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path/subdirectory").Should().Be(true);
    }

    [Fact]
    public async Task MatchedUserAgent_BothAllowAndDisallowSamePath_PreferAllowRule()
    {
        // Arrange
        var file =
@"User-agent: SomeBot
Allow: /some/path
Disallow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(true);
    }

    [Fact]
    public async Task MatchedUserAgent_BothDisallowAndAllowSamePath_PreferAllowRule()
    {
        // Arrange
        var file =
@"User-agent: SomeBot
Disallow: /some/path
Allow: /some/path
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(true);
    }

    [Fact]
    public async Task MatchedUserAgent_DisallowPathWrongCase_Allow()
    {
        // Arrange
        var file =
@"User-agent: SomeBot
Disallow: /SoMe/paTh
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.TryGetRules(ProductToken.Parse("SomeBot"), out var ruleChecker);
        robotsTxt.Should().NotBe(null);
        ruleChecker!.IsAllowed("/some/path").Should().Be(true);
    }
}
