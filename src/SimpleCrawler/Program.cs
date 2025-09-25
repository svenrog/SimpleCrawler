using CommandLine;
using HtmlAgilityPack;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SimpleCrawler.Data;
using SimpleCrawler.Extensions;
using System.Diagnostics.CodeAnalysis;

namespace SimpleCrawler;

internal static class Program
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Options))]
    private static async Task Main(string[] arguments)
    {
        var builder = Host.CreateApplicationBuilder(arguments);
        var parseResult = Parser.Default.ParseArguments<Options>(arguments);

        builder.Services.AddSingleton<HtmlWeb>();
        builder.Services.AddSingleton<Crawler>();
        builder.Logging.AddConsoleLogging();

        await parseResult.WithParsedAsync(async options =>
        {
            builder.Services.AddSingleton(options);
            await Run(builder);
        });

        parseResult.WithNotParsed((errors) =>
        {
            Fail(builder, errors);
        });
    }

    private static async Task Run(HostApplicationBuilder builder)
    {
        using var host = builder.Build();
        using var tokenSource = new CancellationTokenSource();

        Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e)
        {
            tokenSource.Cancel();
        };

        var logger = host.Services.GetRequiredService<ILogger<Crawler>>();
        var options = host.Services.GetRequiredService<Options>();
        var crawler = host.Services.GetRequiredService<Crawler>();
        var result = await crawler.Scrape(tokenSource.Token);

        await File.WriteAllLinesAsync(options.Output, result.Urls, tokenSource.Token);

        logger.LogInformation("Wrote output file to '{path}'", options.Output);
    }

    private static void Fail(HostApplicationBuilder builder, IEnumerable<Error> errors)
    {
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<Crawler>>();

        foreach (var error in errors)
        {
            if (error is UnknownOptionError unknownError)
            {
                logger.LogError("{tag}: {token}", unknownError.Tag, unknownError.Token);
            }
            else if (error is MissingRequiredOptionError missingRequired)
            {
                logger.LogError("{tag}: {name}",
                    missingRequired.Tag,
                    missingRequired.NameInfo.NameText);
            }
            else
            {
                logger.LogError("{tag}", error.Tag);
            }
        }
    }
}
