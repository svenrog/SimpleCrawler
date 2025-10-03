using Crawler.TestHost.Infrastructure.Factories;

namespace Crawler.TestHost;

public partial class Program
{
    public static void Main(string[] args)
    {
        var app = SpaWebApplicationFactory.Create();
        app.Run();
    }
}