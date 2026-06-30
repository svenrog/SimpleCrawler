using Crawler.Js.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace Crawler.Js.HtmlAgilityPack;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddHtmlAgilityPackHtmlParser(this IServiceCollection services)
    {
        services.AddSingleton<IHtmlParser, HtmlAgilityPackHtmlParser>();
        return services;
    }
}
