using Crawler.TestHost.Infrastructure.Routing;
using Microsoft.AspNetCore.StaticFiles;

namespace Crawler.TestHost.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static void AddSpaServices(this IServiceCollection services)
    {
        services.AddSingleton(new FileExtensionContentTypeProvider(FileExtensions.MimeTypes));
        services.AddSingleton<EmbeddedResourceRouteResolver>();
    }
}
