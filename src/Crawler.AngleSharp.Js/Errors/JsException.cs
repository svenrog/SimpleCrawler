namespace Crawler.AngleSharp.Js.Errors;

public sealed class JsException : Exception
{
    public JsException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
