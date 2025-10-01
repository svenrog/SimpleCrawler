using Crawler.TestHost.Infrastructure.Extensions;
using Crawler.TestHost.Infrastructure.Results;

namespace Crawler.TestHost;

public partial class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateSlimBuilder(args);
        var defaultHtml = ResourceHelper.GetResponse("default")
            ?? throw new InvalidOperationException("Default response cannot be null here");

        var app = builder.Build();

#pragma warning disable ASP0018 // Unused route parameter
        app.MapGet("/{*path}", () => Results.Extensions.Html(defaultHtml));
#pragma warning restore ASP0018 // Unused route parameter
        app.Run();
    }
}