namespace Crawler.TestHost.Infrastructure.Results;

public sealed class RawResult : IResult
{
    private readonly byte[] _bytes;
    private readonly string _mimeType;

    public RawResult(byte[] bytes, string mimeType)
    {
        _bytes = bytes;
        _mimeType = mimeType;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = _mimeType;
        httpContext.Response.ContentLength = _bytes.Length;

        return httpContext.Response.Body.WriteAsync(_bytes, 0, _bytes.Length);
    }
}
