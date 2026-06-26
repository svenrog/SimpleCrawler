namespace Crawler.AngleSharp.Js.Errors;

public sealed class SpaScriptException : Exception
{
    public SpaScriptException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
