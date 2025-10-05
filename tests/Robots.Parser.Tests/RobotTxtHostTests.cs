using FluentAssertions;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Robots.Txt.Parser.Tests.Unit;

public partial class RobotsTxtParserTests
{
    [Fact]
    public async Task TryGetHost_HostSpecified_ReturnTrue()
    {
        // Arrange
        var file =
@"Host: robots.github.com

User-agent: *
Disallow: /
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.Should().NotBe(null);
        robotsTxt.TryGetHost(out var host).Should().Be(true);
        host.Should().Be("robots.github.com");
    }

    [Fact]
    public async Task TryGetHost_NonStandardCaseHostSpecified_ReturnTrue()
    {
        // Arrange
        var file =
@"host: robots.github.com

User-agent: *
Disallow: /
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.Should().NotBe(null);
        robotsTxt.TryGetHost(out var host).Should().Be(true);
        host.Should().Be("robots.github.com");
    }

    [Fact]
    public async Task TryGetHost_FullyQualifiedHostSpecified_ReturnTrue()
    {
        // Arrange
        var file =
@"Host: https://robots.github.com

User-agent: *
Disallow: /
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.Should().NotBe(null);
        robotsTxt.TryGetHost(out var host).Should().Be(true);
        host.Should().Be("robots.github.com");
    }
}
