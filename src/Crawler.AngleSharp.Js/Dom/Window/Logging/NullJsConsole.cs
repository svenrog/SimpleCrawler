namespace Crawler.AngleSharp.Js.Dom.Window.Logging;

public sealed class NullJsConsole : IJsConsole
{
    public void log(params object?[] args) { }
    public void info(params object?[] args) { }
    public void warn(params object?[] args) { }
    public void error(params object?[] args) { }
    public void debug(params object?[] args) { }
    public void trace(params object?[] args) { }
    public void dir(params object?[] args) { }
    public void dirxml(params object?[] args) { }
    public void group(params object?[] args) { }
    public void groupCollapsed(params object?[] args) { }
    public void groupEnd(params object?[] args) { }
    public void table(params object?[] args) { }
    public void assert(params object?[] args) { }
    public void count(params object?[] args) { }
    public void countReset(params object?[] args) { }
    public void time(params object?[] args) { }
    public void timeEnd(params object?[] args) { }
    public void timeLog(params object?[] args) { }
    public void clear(params object?[] args) { }
}
