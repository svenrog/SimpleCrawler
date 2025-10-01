using BenchmarkDotNet.Running;

namespace Crawler.Benchmarks;

internal class Program
{
    static void Main(string[] args)
    {
        BenchmarkRunner.Run(typeof(Program).Assembly);

        /*
        var benchmarks = new Benchmarks();

        benchmarks.Setup();
        await benchmarks.HtmlAgilityPackCrawl();
        await benchmarks.Cleanup();
        */
    }
}
