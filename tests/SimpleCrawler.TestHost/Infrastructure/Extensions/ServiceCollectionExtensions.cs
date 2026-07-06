using SimpleCrawler.TestHost.Infrastructure.Routing;
using Microsoft.AspNetCore.StaticFiles;

namespace SimpleCrawler.TestHost.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    private static readonly Dictionary<string, string> _spaMimeTypes = new()
    {
        { ".html", "text/html" },
        { ".css", "text/css" },
        { ".txt", "text/plain" },
        { ".xml", "text/xml" },
        { ".woff2", "font/woff2" },
        { ".js", "text/javascript" },
        { ".png", "image/png" },
        { ".svg", "image/svg+xml" },
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
