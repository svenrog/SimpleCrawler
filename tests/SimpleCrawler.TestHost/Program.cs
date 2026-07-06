using SimpleCrawler.TestHost.Infrastructure.Factories;

namespace SimpleCrawler.TestHost;

public partial class Program
{
    public static void Main(string[] args)
    {
        var framework = args.FirstOrDefault();
        var app = framework is null
            ? StaticWebApplicationFactory.Create()
            : SpaWebApplicationFactory.Create(framework: framework);

        app.Run();
    }
}
