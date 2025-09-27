using CommandLine;

namespace SimpleCrawler;

public sealed class Options
{
    [Option('e', "entryPoint", Required = true, Default = "http://127.0.0.1/", HelpText = "First page to visit")]
    public string Entry { get; set; } = "http://127.0.0.1/";

    [Option('c', "cookie", Required = false, HelpText = "Sets cookie header")]
    public string? Cookie { get; set; }

    [Option('o', "outputFile", Required = true, HelpText = "The file to output to.")]
    public string Output { get; set; } = string.Empty;

    [Option('t', "threads", Required = false, Default = 8, HelpText = "Parallel pages to fetch.")]
    public int Parallellism { get; set; } = 8;

    [Option('m', "maxPages", Required = false, Default = 10000, HelpText = "Max pages to visit.")]
    public int MaxPages { get; set; } = 10000;
}
