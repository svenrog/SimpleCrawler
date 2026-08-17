using Microsoft.Extensions.Logging;
using SimpleCrawler.Js.Errors;

namespace SimpleCrawler.Js.Rendering;

/// <summary>
/// One page's policy at every crossing into the JS engine, stated once instead of per call site: a stopped
/// run stops, and anything else costs the crossing that raised it rather than the render.
/// <list type="bullet">
/// <item><see cref="OperationCanceledException"/> and <see cref="JsPageTimeoutException"/> propagate — the
/// first is the caller stopping the run, the second a ceiling that bounds the page itself.</item>
/// <item>Everything else — a <see cref="JsException"/> from page code, a per-script
/// <see cref="TimeoutException"/>, a raw CLR exception from host code the engine ran — is logged as a
/// warning and the render continues with the failure's declared fallback.</item>
/// </list>
/// <para>
/// The warning is the point as much as the isolation is: a consumer counting the renderer's warnings can
/// still tell a page that ran in full from one that did not. The <c>what</c> argument names the crossing and
/// the templates read <c>"&lt;what&gt; error on '&lt;url&gt;'"</c> for page code and
/// <c>"&lt;what&gt; failed on '&lt;url&gt;'"</c> for everything else, which is the text a log-matching
/// consumer keys on — <c>docs/RENDER-ISSUES.md</c> in the Overlode repo is one.
/// </para>
/// </summary>
internal sealed class RenderIsolation
{
    /// <summary>
    /// How many per-script ceilings one page may spend before it is abandoned as a whole. A ceiling that is
    /// per execution call cannot bound a page on its own — a page of many hanging scripts would spend it
    /// once each — so the count is what turns a repeated per-script timeout back into a page-level stop.
    /// </summary>
    private const int _maxSpentScriptCeilings = 3;

    private readonly ILogger _logger;
    private readonly string _pageUrl;
    private int _spentCeilings;

    public RenderIsolation(ILogger logger, string pageUrl)
    {
        _logger = logger;
        _pageUrl = pageUrl;
    }

    /// <summary>
    /// Runs a crossing that produces nothing, returning whether it completed.
    /// </summary>
    public bool Run(string what, Action cross)
        => Run(what, () => { cross(); return true; }, false);

    /// <summary>
    /// Runs a crossing that produces a value, answering <paramref name="onFailure"/> when it did not
    /// complete. The fallback is the caller's statement of what the render is worth without this crossing.
    /// </summary>
    public T Run<T>(string what, Func<T> cross, T onFailure)
    {
        try
        {
            return cross();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsPageTimeoutException)
        {
            throw;
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning("{what} exceeded the script ceiling on '{url}': {message}", what, _pageUrl, ex.Message);
            if (++_spentCeilings > _maxSpentScriptCeilings)
                throw new JsPageTimeoutException($"The page spent {_spentCeilings} script ceilings; abandoning the render.", ex);

            return onFailure;
        }
        catch (JsException ex)
        {
            _logger.LogWarning("{what} error on '{url}': {message}\n{details}", what, _pageUrl, ex.Message, ex.ErrorDetails);
            return onFailure;
        }
        // Host code the engine called (a module fetch, a parse, an embedded function) throws raw CLR
        // exceptions the engine never surfaces as a JsException. They are no more fatal to the page than a
        // JS error is: it is where they escaped to that used to make them so.
        catch (Exception ex)
        {
            _logger.LogWarning("{what} failed on '{url}': {message}", what, _pageUrl, ex.Message);
            return onFailure;
        }
    }
}
