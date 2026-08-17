namespace SimpleCrawler.Js.Errors;

/// <summary>
/// A ceiling that bounds the <b>page</b> rather than one execution call: the render cannot continue, because
/// what is spent is not something a single script can be charged for.
/// <para>
/// Both scopes report as <see cref="TimeoutException"/> — this derives from it so a caller that classifies a
/// timeout keeps working — but the renderer isolates a per-script ceiling (the script is abandoned, the page
/// runs on) and re-raises this one. Without the distinction, a page budget enforced by interrupting whatever
/// happens to be executing would be absorbed as that script's own failure and stop nothing.
/// </para>
/// </summary>
public sealed class JsPageTimeoutException : TimeoutException
{
    public JsPageTimeoutException(string message)
        : base(message)
    {
    }

    public JsPageTimeoutException(string message, Exception? innerException)
        : base(message, innerException)
    {
    }
}
