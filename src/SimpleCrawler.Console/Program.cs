using CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Simple.Logging.Console.Extensions;
using SimpleCrawler.Console.Extensions;
using SimpleCrawler.Console.Serialization;
using SimpleCrawler.Core;
using SimpleCrawler.Core.Models;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
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
            settings.HelpWriter = SystemConsole.Error;
            settings.CaseInsensitiveEnumValues = true;
            settings.AllowMultiInstance = true;
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

        var logger = host.Services.GetRequiredService<ILogger<ICrawler>>();
        var options = host.Services.GetRequiredService<Options>();
        var crawler = host.Services.GetRequiredService<ICrawler>();

        var entries = options.Entry.ToList();
        if (entries.Count == 0)
        {
            logger.LogError("At least one entry point (-e) is required.");
            return;
        }

        var cancelled = false;
        SystemConsole.CancelKeyPress += OnCancel;
        try
        {
            var result = await crawler.Start(entries, tokenSource.Token);

            await File.WriteAllLinesAsync(options.Output, result.Urls, tokenSource.Token);

            logger.LogInformation("Wrote output file to '{path}'", options.Output);

            if (!string.IsNullOrWhiteSpace(options.Report))
            {
                await WriteReport(options.Report, result.Reports, tokenSource.Token);
                logger.LogInformation("Wrote report file to '{path}'", options.Report);
            }
        }
        catch (OperationCanceledException) when (tokenSource.IsCancellationRequested)
        {
            logger.LogInformation("Crawl interrupted; checkpoint saved. Re-run to resume from where it stopped.");
        }
        finally
        {
            SystemConsole.CancelKeyPress -= OnCancel;
        }

        void OnCancel(object? sender, ConsoleCancelEventArgs e)
        {
            if (cancelled)
            {
                e.Cancel = false;
                return;
            }

            cancelled = true;
            e.Cancel = true;
            tokenSource.Cancel();
            logger.LogInformation("Cancellation requested; finishing in-flight requests and saving checkpoint...");
        }
    }

    private static async Task WriteReport(string path, IReadOnlyCollection<UrlReport> reports, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(reports, CrawlerJsonContext.Default.IReadOnlyCollectionUrlReport);
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private static void Fail(HostApplicationBuilder builder, IEnumerable<Error> errors)
    {
        using var host = builder.Build();
        var logger = host.Services.GetRequiredService<ILogger<ICrawler>>();

        logger.LogCliErrors(errors);
    }
}
