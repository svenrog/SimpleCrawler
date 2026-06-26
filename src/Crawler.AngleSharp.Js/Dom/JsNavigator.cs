namespace Crawler.AngleSharp.Js.Dom;

public sealed class JsNavigator
{
    private readonly JsGeolocation _geolocation = new();

    public string userAgent => "Mozilla/5.0 (compatible; SimpleCrawler; +AngleSharp.Js)";
    public string language => "en-US";
    public string platform => string.Empty;
    public bool onLine => true;
    public object geolocation => _geolocation;
}
