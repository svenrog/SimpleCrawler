using Jint;

namespace Crawler.Js.Jint;

// Pairs a pooled Jint engine with its rebindable module loader, whether its realm has had the DOM prelude
// installed yet, and its page count so the pool can retire it after a fixed number of uses.
internal sealed class JintEngineLease
{
    public JintEngineLease(Engine engine, JintModuleLoader loader)
    {
        Engine = engine;
        Loader = loader;
    }

    public Engine Engine { get; }

    public JintModuleLoader Loader { get; }

    // False until the first page installs dom.js on this realm; once true, subsequent pages reset instead.
    public bool Initialized { get; set; }

    public int Uses { get; set; }
}
