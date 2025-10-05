using Crawler.Core.Robots;
using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Moq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Robots.Txt.Parser.Tests.Unit;

public partial class RobotsTxtParserTests
{
    private readonly Mock<IRobotClient> _robotsClientMock;
    private readonly RobotsTxtParser _parser;

    public RobotsTxtParserTests()
    {
        _robotsClientMock = new Mock<IRobotClient>();
        _parser = new RobotsTxtParser(_robotsClientMock.Object);
    }

    [Fact]
    public async Task ReadFromStreamAsync_EmptyFile_LoadDefault()
    {
        // Arrange
        var file = "";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.Should().NotBe(null);
    }

    [Fact]
    public async Task ReadFromStreamAsync_WithLineComments_CommentsIgnored()
    {
        // Arrange
        var file =
@"# This is a basic robots.txt file
User-agent: *
Disallow: /
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.Should().NotBe(null);
    }

    [Fact]
    public async Task ReadFromStreamAsync_WithEndOfLineComments_CommentsIgnored()
    {
        // Arrange
        var file =
@"User-agent: * # This line specifies any user agent
Disallow: / # Directs the crawler to ignore the entire website
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.Should().NotBe(null);
    }

    [Fact]
    public async Task ReadFromStreamAsync_Over500KiB_ThrowRobotsTxtException()
    {
        // Arrange
        var fileProvider = new EmbeddedFileProvider(Assembly.GetExecutingAssembly());
        await using var stream = fileProvider.GetFileInfo("over-500kib-robots.txt").CreateReadStream();

        // Act
        var parse = async () => await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        await parse.Should().ThrowAsync<RobotsTxtException>();
    }

    [Fact]
    public async Task ReadFromStreamAsync_InvalidProductToken_Ignore()
    {
        // Arrange
        var file =
@"User-agent: *
Disallow: /

User-agent: InvalidProductToken5
Disallow: 

User-agent: ValidProductToken
Disallow: 
";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        // Act
        var robotsTxt = await _parser.ReadFromStreamAsync(stream, TestContext.Current.CancellationToken);

        // Assert
        robotsTxt.Should().NotBe(null);
    }
}
