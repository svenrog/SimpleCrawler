using SimpleCrawler.Core.Extensions;

namespace SimpleCrawler.Tests;

public class MaxResponseBodySizeTests
{
    [Fact]
    public async Task Returns_Body_When_Under_Cap()
    {
        var bytes = new byte[100];
        Random.Shared.NextBytes(bytes);
        using var content = new ByteArrayContent(bytes);

        var result = await content.ReadCappedByteArrayAsync(1000, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(bytes, result);
    }

    [Fact]
    public async Task Returns_Body_At_Exact_Cap()
    {
        var bytes = new byte[256];
        Random.Shared.NextBytes(bytes);
        using var content = new ByteArrayContent(bytes);

        var result = await content.ReadCappedByteArrayAsync(256, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(256, result!.Length);
    }

    [Fact]
    public async Task Returns_Null_When_Over_Cap()
    {
        var bytes = new byte[1000];
        using var content = new ByteArrayContent(bytes);

        var result = await content.ReadCappedByteArrayAsync(256, CancellationToken.None);

        Assert.Null(result);
    }
}
