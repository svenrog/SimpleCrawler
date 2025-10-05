using Crawler.Core.Robots;
using FluentAssertions;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Robots.Parser.Tests;

public class SitemapParserTests
{
    [Fact]
    public async Task ReadFromStreamAsync_EmptyFile_ThrowSitemapException()
    {
        // Arrange
        var file = @"";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var parse = async () => await SitemapParser.ReadFromStreamAsync(stream, null, cancellationToken);

        // Assert
        await parse.Should().ThrowExactlyAsync<SitemapException>();
    }

    [Fact]
    public async Task ReadFromStreamAsync_ImproperXmlFormat_ThrowSitemapException()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<invalid xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <sitemap>
        <loc>https://www.github.com/organisations.xml</loc>
        <lastmod>2023-08-23</lastmod>
    </sitemap>
</invalid>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var parse = async () => await SitemapParser.ReadFromStreamAsync(stream, null, cancellationToken);

        // Assert
        await parse.Should().ThrowExactlyAsync<SitemapException>();
    }

    [Fact]
    public async Task ParseSitemapIndex_IncorrectLocationFormat_ThrowSitemapException()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<sitemapindex xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <sitemap>
        <loc>invalid[/]location</loc>
    </sitemap>
</sitemapindex>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        var cancellationToken = TestContext.Current.CancellationToken;

        var sitemap = (SitemapIndex)await SitemapParser.ReadFromStreamAsync(stream, null, cancellationToken);

        // Act
        var parse = async () => await sitemap.SitemapUris.ToListAsync();

        // Assert
        await parse.Should().ThrowExactlyAsync<SitemapException>();
    }

    [Fact]
    public async Task ParseSitemapIndex_IncorrectDateFormat_ThrowSitemapException()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<sitemapindex xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <sitemap>
        <loc>https://www.github.com/organisations.xml</loc>
        <lastmod>not-a-real-date</lastmod>
    </sitemap>
</sitemapindex>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));
        var cancellationToken = TestContext.Current.CancellationToken;

        var sitemap = (SitemapIndex)await SitemapParser.ReadFromStreamAsync(stream, null, cancellationToken);

        // Act
        var parse = async () => await sitemap.SitemapUris.ToListAsync(cancellationToken);

        // Assert
        await parse.Should().ThrowExactlyAsync<SitemapException>();
    }

    [Fact]
    public async Task ParseUrlSet_IncorrectDateFormat_ThrowSitemapException()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <url>
        <loc>https://www.github.com/organisations.xml</loc>
        <lastmod>not-a-real-date</lastmod>
    </url>
</urlset>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));
        var cancellationToken = TestContext.Current.CancellationToken;

        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, null, cancellationToken);

        // Act
        var parse = async () => await sitemap.UrlSet.ToListAsync(cancellationToken);

        // Assert
        await parse.Should().ThrowExactlyAsync<SitemapException>();
    }

    [Fact]
    public async Task ParseUrlSet_IncorrectChangeFrequencyFormat_ThrowSitemapException()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <url>
        <loc>https://www.github.com/organisations.xml</loc>
        <changfreq>1</changefreq>
    </url>
</urlset>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));
        var cancellationToken = TestContext.Current.CancellationToken;

        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, null, cancellationToken);

        // Act
        var parse = async () => await sitemap.UrlSet.ToListAsync();

        // Assert
        await parse.Should().ThrowExactlyAsync<SitemapException>();
    }

    [Fact]
    public async Task ParseUrlSet_IncorrectPriorityFormat_ThrowSitemapException()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <url>
        <loc>https://www.github.com/organisations.xml</loc>
        <priority>high</priority>
    </url>
</urlset>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));
        var cancellationToken = TestContext.Current.CancellationToken;

        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, null, cancellationToken);

        // Act
        var parse = async () => await sitemap.UrlSet.ToListAsync();

        // Assert
        await parse.Should().ThrowExactlyAsync<SitemapException>();
    }

    [Fact]
    public async Task ReadFromStreamAsync_SitemapIndexNoModifiedDateFilter_ParseCorrectly()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<sitemapindex xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <sitemap>
        <loc>https://www.github.com/organisations.xml</loc>
        <lastmod>2023-08-23</lastmod>
    </sitemap>
    <sitemap>
        <loc>https://www.github.com/people.xml</loc>
        <lastmod>2023-10-01</lastmod>
    </sitemap>
</sitemapindex>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, null, cancellationToken);

        // Assert
        var sitemapRoot = sitemap.Should().BeOfType<SitemapIndex>().Subject;
        var urlSet = await sitemap.UrlSet.ToListAsync(cancellationToken);
        var sitemapUris = await sitemapRoot.SitemapUris.ToListAsync(cancellationToken);
        urlSet.Should().BeEmpty();
        sitemapUris.Should().BeEquivalentTo(
        [
            new Uri("https://www.github.com/organisations.xml"),
            new Uri("https://www.github.com/people.xml"),
        ]);
    }

    [Fact]
    public async Task ReadFromStreamAsync_SitemapIndexEarlierModifiedDateFilter_ParseCorrectly()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<sitemapindex xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <sitemap>
        <loc>https://www.github.com/organisations.xml</loc>
        <lastmod>2023-08-23</lastmod>
    </sitemap>
    <sitemap>
        <loc>https://www.github.com/people.xml</loc>
        <lastmod>2023-10-01</lastmod>
    </sitemap>
</sitemapindex>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, new DateTime(2023, 08, 22), cancellationToken);

        // Assert
        var sitemapRoot = sitemap.Should().BeOfType<SitemapIndex>().Subject;
        var urlSet = await sitemap.UrlSet.ToListAsync(cancellationToken);
        var sitemapUris = await sitemapRoot.SitemapUris.ToListAsync(cancellationToken);
        urlSet.Should().BeEmpty();
        sitemapUris.Should().BeEquivalentTo(new[]
        {
            new Uri("https://www.github.com/organisations.xml"),
            new Uri("https://www.github.com/people.xml"),
        });
    }

    [Fact]
    public async Task ReadFromStreamAsync_SitemapIndexSameModifiedDateFilter_ParseCorrectly()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<sitemapindex xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <sitemap>
        <loc>https://www.github.com/organisations.xml</loc>
        <lastmod>2023-08-23</lastmod>
    </sitemap>
    <sitemap>
        <loc>https://www.github.com/people.xml</loc>
        <lastmod>2023-10-01</lastmod>
    </sitemap>
</sitemapindex>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, new DateTime(2023, 08, 23), cancellationToken);

        // Assert
        var sitemapRoot = sitemap.Should().BeOfType<SitemapIndex>().Subject;
        var urlSet = await sitemap.UrlSet.ToListAsync(cancellationToken);
        var sitemapUris = await sitemapRoot.SitemapUris.ToListAsync(cancellationToken);
        urlSet.Should().BeEmpty();
        sitemapUris.Should().BeEquivalentTo(
        [
            new Uri("https://www.github.com/organisations.xml"),
            new Uri("https://www.github.com/people.xml"),
        ]);
    }

    [Fact]
    public async Task ReadFromStreamAsync_SitemapIndexExceedsModifiedDateFilter_ParseCorrectly()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8""?>
<sitemapindex xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <sitemap>
        <loc>https://www.github.com/organisations.xml</loc>
        <lastmod>2023-08-23</lastmod>
    </sitemap>
    <sitemap>
        <loc>https://www.github.com/people.xml</loc>
        <lastmod>2023-10-01</lastmod>
    </sitemap>
</sitemapindex>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));
        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, new DateTime(2023, 08, 24), cancellationToken);

        // Assert
        var sitemapRoot = sitemap.Should().BeOfType<SitemapIndex>().Subject;
        var urlSet = await sitemap.UrlSet.ToListAsync(cancellationToken);
        var sitemapUris = await sitemapRoot.SitemapUris.ToListAsync(cancellationToken);
        urlSet.Should().BeEmpty();
        sitemapUris.Should().BeEquivalentTo(new[] { new Uri("https://www.github.com/people.xml") });
    }

    [Fact]
    public async Task ReadFromStreamAsync_UrlSetLocationOnlyNoModifiedDateFilter_ParseCorrectly()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <url>
        <loc>https://www.github.com/drmathias</loc>
    </url>
    <url>
        <loc>https://www.github.com/drmathias/Robots.Txt.Parser</loc>
    </url>
</urlset>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, null, cancellationToken);

        // Assert
        var urlSet = await sitemap.UrlSet.ToListAsync(cancellationToken);
        urlSet.Should().BeEquivalentTo(new[]
        {
            new UrlSetItem(new Uri("https://www.github.com/drmathias"),  null, null, null),
            new UrlSetItem(new Uri("https://www.github.com/drmathias/Robots.Txt.Parser"), null, null, null),
        });
    }

    [Fact]
    public async Task ReadFromStreamAsync_UrlSetLocationOnlyModifiedDateFilter_ParseCorrectly()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <url>
        <loc>https://www.github.com/drmathias</loc>
    </url>
    <url>
        <loc>https://www.github.com/drmathias/Robots.Txt.Parser</loc>
    </url>
</urlset>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, new DateTime(2024, 01, 01), cancellationToken);

        // Assert
        var urlSet = await sitemap.UrlSet.ToListAsync(cancellationToken);
        urlSet.Should().BeEquivalentTo(
        [
            new UrlSetItem(new Uri("https://www.github.com/drmathias"),  null, null, null),
            new UrlSetItem(new Uri("https://www.github.com/drmathias/Robots.Txt.Parser"), null, null, null),
        ]);
    }

    [Fact]
    public async Task ReadFromStreamAsync_UrlSetAllPropertiesNoFilter_ParseCorrectly()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <url>
        <loc>https://www.github.com/drmathias</loc>
        <lastmod>2023-06-01</lastmod>
        <changefreq>daily</changefreq>
        <priority>0.8</priority>
    </url>
    <url>
        <loc>https://www.github.com/drmathias/Robots.Txt.Parser</loc>
        <lastmod>2023-05-12</lastmod>
        <changefreq>monthly</changefreq>
        <priority>0.5</priority>
    </url>
</urlset>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, null, cancellationToken);

        // Assert
        var urlSet = await sitemap.UrlSet.ToListAsync(cancellationToken);
        urlSet.Should().BeEquivalentTo(
        [
            new UrlSetItem(new Uri("https://www.github.com/drmathias"),  new DateTime(2023, 06, 01), ChangeFrequency.Daily, 0.8m),
            new UrlSetItem(new Uri("https://www.github.com/drmathias/Robots.Txt.Parser"), new DateTime(2023, 05, 12), ChangeFrequency.Monthly, 0.5m),
        ]);
    }

    [Fact]
    public async Task ReadFromStreamAsync_UrlSetAllPropertiesEarlierModifiedDateFilter_ParseCorrectly()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <url>
        <loc>https://www.github.com/drmathias</loc>
        <lastmod>2023-06-01</lastmod>
        <changefreq>daily</changefreq>
        <priority>0.8</priority>
    </url>
    <url>
        <loc>https://www.github.com/drmathias/Robots.Txt.Parser</loc>
        <lastmod>2023-05-12</lastmod>
        <changefreq>monthly</changefreq>
        <priority>0.5</priority>
    </url>
</urlset>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, new DateTime(2023, 01, 01), cancellationToken);

        // Assert
        var urlSet = await sitemap.UrlSet.ToListAsync(cancellationToken);
        urlSet.Should().BeEquivalentTo(
        [
            new UrlSetItem(new Uri("https://www.github.com/drmathias"),  new DateTime(2023, 06, 01), ChangeFrequency.Daily, 0.8m),
            new UrlSetItem(new Uri("https://www.github.com/drmathias/Robots.Txt.Parser"), new DateTime(2023, 05, 12), ChangeFrequency.Monthly, 0.5m),
        ]);
    }

    [Fact]
    public async Task ReadFromStreamAsync_UrlSetAllPropertiesEqualModifiedDateFilter_ParseCorrectly()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <url>
        <loc>https://www.github.com/drmathias</loc>
        <lastmod>2023-06-01</lastmod>
        <changefreq>daily</changefreq>
        <priority>0.8</priority>
    </url>
    <url>
        <loc>https://www.github.com/drmathias/Robots.Txt.Parser</loc>
        <lastmod>2023-05-12</lastmod>
        <changefreq>monthly</changefreq>
        <priority>0.5</priority>
    </url>
</urlset>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, new DateTime(2023, 05, 12), cancellationToken);

        // Assert
        var urlSet = await sitemap.UrlSet.ToListAsync(cancellationToken);
        urlSet.Should().BeEquivalentTo(
        [
            new UrlSetItem(new Uri("https://www.github.com/drmathias"),  new DateTime(2023, 06, 01), ChangeFrequency.Daily, 0.8m),
            new UrlSetItem(new Uri("https://www.github.com/drmathias/Robots.Txt.Parser"), new DateTime(2023, 05, 12), ChangeFrequency.Monthly, 0.5m),
        ]);
    }

    [Fact]
    public async Task ReadFromStreamAsync_UrlSetAllPropertiesLaterModifiedDateFilter_ParseCorrectly()
    {
        // Arrange
        var file =
@"<?xml version=""1.0"" encoding=""UTF-8"" ?>
<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    <url>
        <loc>https://www.github.com/drmathias</loc>
        <lastmod>2023-06-01</lastmod>
        <changefreq>daily</changefreq>
        <priority>0.8</priority>
    </url>
    <url>
        <loc>https://www.github.com/drmathias/Robots.Txt.Parser</loc>
        <lastmod>2023-05-12</lastmod>
        <changefreq>monthly</changefreq>
        <priority>0.5</priority>
    </url>
</urlset>";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(file));

        var cancellationToken = TestContext.Current.CancellationToken;

        // Act
        var sitemap = await SitemapParser.ReadFromStreamAsync(stream, new DateTime(2023, 05, 13), cancellationToken);

        // Assert
        var urlSet = await sitemap.UrlSet.ToListAsync(cancellationToken);
        urlSet.Should().BeEquivalentTo(
        [
            new UrlSetItem(new Uri("https://www.github.com/drmathias"),  new DateTime(2023, 06, 01), ChangeFrequency.Daily, 0.8m),
        ]);
    }
}