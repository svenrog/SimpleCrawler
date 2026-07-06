using Crawler.Js.Helpers;

namespace Crawler.Js.Jint;

internal static class Shim
{
    public static readonly string Source = PreludeHelper.LoadSource(typeof(Shim), "shims.js");
}
