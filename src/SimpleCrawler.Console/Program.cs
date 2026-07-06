using CommandLine;
using Crude.Logging.Console.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using SimpleCrawler.Console.Extensions;
using SimpleCrawler.Core;
using System.Diagnostics.CodeAnalysis;
using SystemConsole = System.Console;

namespace SimpleCrawler.Console;

internal static class Program
{
    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicProperties, typeof(Options))]
    private static async Task Main(string[] arguments)
    {
        var builder = Host.CreateApplicationBuilder(arguments);

        builder.Logging.AddConsoleLogging();

        using var parser = new Parser(settings =>
        {
            settings.HelpWriter = System.Console.Error;
            settings.CaseInsensitiveEnumValues = true;
        });

        var parseResult = parser.ParseArguments<Options>(arguments);

        await parseResult.WithParsedAsync(async options =>
        {
            builder.Services.AddCrawler(options);
            builder.Services.RemoveAll<IHttpMessageHandlerBuilderFilter>();

            await Run(builder);
        });
        parseResult.WithNotParsed((errors) => Fail(builder, errors));
    }

    private static async Task Run(HostApplicationBuilder builder)
    {
        using var host = builder.Build();
        using var tokenSource = new CancellationTokenSource();

        SystemConsole.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e)
        {
            tokenSource.Cancel();
        };

        var logger = host.Services.GetRequiredService<ILogger<ICrawler>>();
        var options = host.Services.GetRequiredService<Options>();
        var crawler = host.Services.GetRequiredService<ICrawler>();

        var result = await crawler.Start(options.Entry, tokenSource.Token);

        await File.WriteAllLinesAsync(options.Output, result.Urls, tokenSource.Token);

        logger.LogInformation("Wrote output file to '{path}'", options.Output);
    }

    private static void Fail(HostApplicationBuilder builder, IEnumerable<Error> errors)
    {
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<ICrawler>>();

        logger.LogCliErrors(errors);
    }
}
