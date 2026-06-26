namespace Crawler.AngleSharp.Js.Errors;

public sealed class JsScriptException : Exception
{
    public JsScriptException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
