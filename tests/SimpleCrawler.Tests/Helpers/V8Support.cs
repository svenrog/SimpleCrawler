using Microsoft.ClearScript.V8;

namespace SimpleCrawler.Tests.Helpers;

internal static class V8Support
{
    public const string UnavailableReason = "ClearScript V8 native library could not be loaded (missing Visual C++ runtime?).";

    public static readonly bool IsAvailable = Probe();

    private static bool Probe()
    {
        try
        {
            using var engine = new V8ScriptEngine();
            return engine.Evaluate("1 + 1") is int result && result == 2;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
