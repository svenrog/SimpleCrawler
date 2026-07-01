using Crawler.Js.Rendering;

namespace Crawler.Js.Helpers;

public static class PreludeHelper
{
    public static PreludeEntry Load(Type type, string fileName)
    {
        var source = LoadSource(type, fileName);
        return new PreludeEntry(fileName, source);
    }

    public static string LoadSource(Type type, string fileName)
    {
        var resourceName = $"{type.Namespace}.Preludes.{fileName}";
        var assembly = type.Assembly;

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded prelude '{resourceName}' was not found in '{assembly.GetName().Name}'. " +
                $"Available resources: {string.Join(", ", assembly.GetManifestResourceNames())}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
