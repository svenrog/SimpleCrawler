using Crawler.TestHost.Infrastructure.Results;
using Microsoft.AspNetCore.StaticFiles;
using System.Reflection;

namespace Crawler.TestHost.Infrastructure.Routing;

public class EmbeddedResourceRouteResolver
{
    private static readonly Assembly _assembly = typeof(EmbeddedResourceRouteResolver).Assembly;
    private static readonly AssemblyName _assemblyName = _assembly.GetName();

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
        var resourceFilter = _assemblyName.Name + ".wwwroot.";
        var resources = new Dictionary<string, byte[]>();

        foreach (var name in names)
        {
            if (!name.StartsWith(resourceFilter))
                continue;

            var resourceFile = name[resourceFilter.Length..];
            var resourceName = Path.GetFileNameWithoutExtension(resourceFile);
            var resourceExtension = Path.GetExtension(resourceFile);
            var resourceKey = '/' + resourceName.Replace('.', '/') + resourceExtension;

            var content = ResourceHelper.GetWebRootResourceBytes(resourceFile);

            resources.Add(resourceKey, content);
        }

        return resources;
    }
}
