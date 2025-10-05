using Crawler.TestHost.Infrastructure.Routing;
using Microsoft.AspNetCore.StaticFiles;

namespace Crawler.TestHost.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    private static readonly Dictionary<string, string> _spaMimeTypes = new()
    {
        { ".html", "text/html" },
        { ".css", "text/css" },
        { ".txt", "text/plain" },
        { ".woff2", "font/woff2" },
        { ".js", "text/javascript" },
        { ".png", "image/png" },
    };

    private static readonly Dictionary<string, string> _staticMimeTypes = new()
    {
        { ".txt", "text/plain" },
        { ".xml", "text/xml" }
    };

    public static void AddSpaServices(this IServiceCollection services)
    {
        services.AddSingleton(new FileExtensionContentTypeProvider(_spaMimeTypes));
        services.AddSingleton<EmbeddedResourceRouteResolver>();
    }

    public static void AddStaticServices(this IServiceCollection services)
    {
        services.AddSingleton(new FileExtensionContentTypeProvider(_staticMimeTypes));
        services.AddSingleton<EmbeddedResourceRouteResolver>();
    }
}
