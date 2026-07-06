using SimpleCrawler.Js.Helpers;

namespace SimpleCrawler.Js.Jint;

internal static class Shim
{
    public static readonly string Source = PreludeHelper.LoadSource(typeof(Shim), "shims.js");
}
