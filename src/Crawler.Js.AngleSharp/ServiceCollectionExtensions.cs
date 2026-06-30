using Crawler.Js.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Js.AngleSharp;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAngleSharpHtmlParser(this IServiceCollection services)
    {
        services.AddSingleton<IHtmlParser, AngleSharpHtmlParser>();
        return services;
    }
}
