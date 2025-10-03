
using System.Net.Mime;
using System.Text;

namespace Crawler.TestHost.Infrastructure.Results;

public sealed class SerializedJsonResult : IResult
{
    private readonly string _json;

    public SerializedJsonResult(string json)
    {
        _json = json;
    }

    public Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.ContentType = MediaTypeNames.Application.Json;
        httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(_json);

        return httpContext.Response.WriteAsync(_json);
    }
}
