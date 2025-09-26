using CommandLine;
using Crawler.Alleima.ETrack;
using Crawler.Alleima.ETrack.Models;
using Crawler.Core.Extensions;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleCrawler.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace SimpleCrawler;

internal static class Program
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Options))]
    private static async Task Main(string[] arguments)
    {
        var builder = Host.CreateApplicationBuilder(arguments);

        builder.Services.AddSingleton<HtmlWeb>();
        builder.Services.AddSingleton<AlleimaCrawler>();
        builder.Logging.AddConsoleLogging();

        var parseResult = Parser.Default.ParseArguments<Options>(arguments);

        await parseResult.WithParsedAsync(async options =>
        {
            builder.Services.AddOptions(options);
            await Run(builder);
        });

        parseResult.WithNotParsed((errors) => Fail(builder, errors));
    }

    private static async Task Run(HostApplicationBuilder builder)
    {
        using var host = builder.Build();
        using var tokenSource = new CancellationTokenSource();

        Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e)
        {
            tokenSource.Cancel();
        };

        var logger = host.Services.GetRequiredService<ILogger<AlleimaCrawler>>();
        var options = host.Services.GetRequiredService<Options>();
        var crawler = host.Services.GetRequiredService<AlleimaCrawler>();
        var result = (AlleimaScrapeResult)await crawler.Scrape(tokenSource.Token);

        await File.WriteAllLinesAsync(options.Output + "-categories.log", result.Categories, tokenSource.Token);
        await File.WriteAllLinesAsync(options.Output + "-products.log", result.Products, tokenSource.Token);
        await File.WriteAllLinesAsync(options.Output + "-variations.log", result.Variations, tokenSource.Token);
        await File.WriteAllLinesAsync(options.Output + "-other.log", result.Other, tokenSource.Token);

        logger.LogInformation("Wrote output file to '{path}'", options.Output);
    }

    private static void Fail(HostApplicationBuilder builder, IEnumerable<Error> errors)
    {
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<AlleimaCrawler>>();

        logger.LogCliErrors(errors);
    }
}
