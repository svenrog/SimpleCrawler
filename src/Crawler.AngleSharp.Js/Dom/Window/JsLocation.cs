using Crawler.AngleSharp.Js.Abstractions;
using Crawler.AngleSharp.Js.Dom.Helpers;

namespace Crawler.AngleSharp.Js.Dom.Window;

public sealed class JsLocation : IJsLocation
{
    internal JsLocation(Uri uri)
    {
        LocationHelper.Apply(this, uri);
    }

    public string href { get; set; } = string.Empty;
    public string origin { get; set; } = string.Empty;
    public string protocol { get; set; } = string.Empty;
    public string host { get; set; } = string.Empty;
    public string hostname { get; set; } = string.Empty;
    public string port { get; set; } = string.Empty;
    public string pathname { get; set; } = string.Empty;
    public string search { get; set; } = string.Empty;
    public string hash { get; set; } = string.Empty;

    public override string ToString() => href;

    internal void Apply(string url)
    {
        LocationHelper.Apply(this, url);
    }
}
