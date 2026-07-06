using System.Diagnostics.CodeAnalysis;

namespace Crawler.TestHost.Infrastructure.Results;

public class RouteResponse
{
    [MemberNotNullWhen(true, nameof(Content))]
    [MemberNotNullWhen(true, nameof(MimeType))]
    public bool Match { get; init; }

    public byte[]? Content { get; init; }
    public string? MimeType { get; init; }


    public static RouteResponse Success(byte[] content, string mimeType) => new() { Match = true, Content = content, MimeType = mimeType };
    public static RouteResponse Fail() => new() { Match = false };
}
