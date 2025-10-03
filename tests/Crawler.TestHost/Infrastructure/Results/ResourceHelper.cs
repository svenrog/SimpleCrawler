namespace Crawler.TestHost.Infrastructure.Results;

public static class ResourceHelper
{
    public static string GetWebRootResource(string resourceFile)
    {
        return GetResponse($"Crawler.TestHost.wwwroot.{resourceFile}");
    }


    public static string GetHtmlResponse(string responseName)
    {
        return GetResponse($"Crawler.TestHost.Response.{responseName}.html");
    }

    public static string GetJsonResponse(string responseName)
    {
        return GetResponse($"Crawler.TestHost.Response.{responseName}.json");
    }

    private static string GetResponse(string resourceName)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Resource with name {resourceName} not found");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
