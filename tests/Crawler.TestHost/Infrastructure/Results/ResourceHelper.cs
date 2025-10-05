using System.Reflection;

namespace Crawler.TestHost.Infrastructure.Results;

public static class ResourceHelper
{
    private static readonly Assembly _assembly = typeof(ResourceHelper).Assembly;
    private static readonly AssemblyName _assemblyName = _assembly.GetName();

    public static byte[] GetWebRootResourceBytes(ReadOnlySpan<char> resourceFile)
    {
        return GetResponseBytes($"{_assemblyName.Name}.wwwroot.{resourceFile}");
    }

    public static string GetWebRootResource(ReadOnlySpan<char> resourceFile)
    {
        return GetResponseString($"{_assemblyName.Name}.wwwroot.{resourceFile}");
    }

    public static string GetHtmlResponse(ReadOnlySpan<char> responseName)
    {
        return GetResponseString($"{_assemblyName.Name}.Response.{responseName}.html");
    }

    public static string GetJsonResponse(ReadOnlySpan<char> responseName)
    {
        return GetResponseString($"{_assemblyName.Name}.Response.{responseName}.json");
    }

    private static string GetResponseString(string resourceName)
    {
        using var stream = GetResponseStream(resourceName);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static byte[] GetResponseBytes(string resourceName)
    {
        using var stream = GetResponseStream(resourceName);
        if (stream.Length > int.MaxValue)
            throw new InvalidOperationException($"Resource {resourceName} is bigger than this application can handle");

        var memoryStream = new MemoryStream((int)stream.Length);
        stream.CopyTo(memoryStream);

        return memoryStream.ToArray();
    }

    private static Stream GetResponseStream(string resourceName)
    {
        return typeof(Program).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource with name {resourceName} not found");
    }
}
