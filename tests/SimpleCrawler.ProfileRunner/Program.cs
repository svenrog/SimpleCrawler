using CommandLine;
using SimpleCrawler.ProfileRunner;

using var parser = new Parser(settings =>
{
    settings.HelpWriter = Console.Error;
    settings.CaseInsensitiveEnumValues = true;
});

var result = parser.ParseArguments<ProfileOptions, RenderSizeOptions>(args);

await result.WithParsedAsync<ProfileOptions>(o => ProfileHarness.Run(o.Combo, o.Iterations, o.Framework));
await result.WithParsedAsync<RenderSizeOptions>(o => ProfileHarness.RenderSize(o.Combo, o.Framework, o.Url, o.EnableFetch, o.EnableStreams));
