namespace Crawler.AngleSharp.Js.Dom.Window.Logging;

public interface IJsConsole
{
    void log(params object?[] args);
    void info(params object?[] args);
    void warn(params object?[] args);
    void error(params object?[] args);
    void debug(params object?[] args);
    void trace(params object?[] args);
    void dir(params object?[] args);
    void dirxml(params object?[] args);
    void group(params object?[] args);
    void groupCollapsed(params object?[] args);
    void groupEnd(params object?[] args);
    void table(params object?[] args);
    void assert(params object?[] args);
    void count(params object?[] args);
    void countReset(params object?[] args);
    void time(params object?[] args);
    void timeEnd(params object?[] args);
    void timeLog(params object?[] args);
    void clear(params object?[] args);
}
