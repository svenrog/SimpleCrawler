namespace Crawler.AngleSharp.Js.Rendering;

internal static class JsPreludes
{
    public static readonly string InstanceShims = Load("instance-shims.js");
    public static readonly string Global = Load("global.js");
    public static readonly string Crypto = Load("crypto.js");
    public static readonly string ResourceEvent = Load("resource-event.js");
    public static readonly string MessageChannel = Load("message-channel.js");
    public static readonly string History = Load("history.js");
    public static readonly string HtmlElement = Load("html-element.js");
    public static readonly string DomGlobals = Load("dom-globals.js");
    public static readonly string Fetch = Load("fetch.js");

    private static string Load(string filename)
    {
        var type = typeof(JsPreludes);
        var resourceName = $"{type.Namespace}.Preludes.{filename}";
        using var stream = type.Assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
