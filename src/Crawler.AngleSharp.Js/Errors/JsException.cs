namespace Crawler.AngleSharp.Js.Errors;

public sealed class JsException : Exception
{
    public JsException(string message, string? errorDetails, Exception innerException)
    : base(message, innerException)
    {
        ErrorDetails = errorDetails;
    }

    public string? ErrorDetails { get; }
}
