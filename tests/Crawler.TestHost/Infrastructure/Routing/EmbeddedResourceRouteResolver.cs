using Crawler.TestHost.Infrastructure.Results;
using Microsoft.AspNetCore.StaticFiles;
using System.Reflection;

namespace Crawler.TestHost.Infrastructure.Routing;

public class EmbeddedResourceRouteResolver
{
    private static readonly Assembly _assembly = typeof(EmbeddedResourceRouteResolver).Assembly;

    private readonly Lazy<Dictionary<string, byte[]>> _resources;
    private readonly FileExtensionContentTypeProvider _extensionProvider;

    public EmbeddedResourceRouteResolver(FileExtensionContentTypeProvider extensionProvider)
    {
        _extensionProvider = extensionProvider;
        _resources = new Lazy<Dictionary<string, byte[]>>(CompileResources);
    }

    public RouteResponse Route(string path)
    {
        if (!_extensionProvider.TryGetContentType(path, out var contentType))
            return RouteResponse.Fail();

        if (!_resources.Value.TryGetValue(path, out var content))
            return RouteResponse.Fail();

        return RouteResponse.Success(content, contentType);
    }

    private Dictionary<string, byte[]> CompileResources()
    {
        var names = _assembly.GetManifestResourceNames();
        const string resourceFilter = "wwwroot/";
        var resources = new Dictionary<string, byte[]>();

        foreach (var name in names)
        {
            if (!name.StartsWith(resourceFilter))
                continue;

            var resourceKey = '/' + name[resourceFilter.Length..];
            var content = ResourceHelper.GetResourceBytes(name);

            resources.Add(resourceKey, content);
        }

        return resources;
    }
}
