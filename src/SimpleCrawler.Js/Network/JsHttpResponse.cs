namespace SimpleCrawler.Js.Network;

public sealed class JsHttpResponse
{
    public int status { get; init; }
    public string statusText { get; init; } = string.Empty;
    public string url { get; init; } = string.Empty;
    public string body { get; init; } = string.Empty;
    public string headersJson { get; init; } = "{}";
    public string? error { get; init; }
}
