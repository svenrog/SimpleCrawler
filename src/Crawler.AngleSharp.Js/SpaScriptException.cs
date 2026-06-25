namespace Crawler.AngleSharp.Js;

public sealed class SpaScriptException : Exception
{
    public SpaScriptException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
