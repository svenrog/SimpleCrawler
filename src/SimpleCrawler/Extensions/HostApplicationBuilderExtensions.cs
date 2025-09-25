using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SimpleCrawler.Extensions;
public static class HostApplicationBuilderExtensions
{

    public static void UseDefaultServiceProvider(this IHostApplicationBuilder builder, Action<ServiceProviderOptions> action)
    {
        var options = new ServiceProviderOptions();

        action(options);

        var serviceProviderFactory = new DefaultServiceProviderFactory(options);

        builder.ConfigureContainer(serviceProviderFactory);
    }
}
