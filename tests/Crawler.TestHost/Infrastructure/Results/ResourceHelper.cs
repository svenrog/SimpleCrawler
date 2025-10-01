namespace Crawler.TestHost.Infrastructure.Results;

public static class ResourceHelper
{
    public static string? GetResponse(string responseName)
    {
        using var stream = typeof(Program).Assembly.GetManifestResourceStream($"Crawler.TestHost.Response.{responseName}.html");
        if (stream == null)
            return null;

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
