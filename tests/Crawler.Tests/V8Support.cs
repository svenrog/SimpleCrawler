using JavaScriptEngineSwitcher.Core;
using JavaScriptEngineSwitcher.V8;

namespace Crawler.Tests;

internal static class V8Support
{
    public const string UnavailableReason = "ClearScript V8 native library could not be loaded (missing Visual C++ runtime?).";

    public static readonly bool IsAvailable = Probe();

    private static bool Probe()
    {
        try
        {
            var switcher = new JsEngineSwitcher();
            switcher.EngineFactories.AddV8();
            switcher.DefaultEngineName = V8JsEngine.EngineName;

            using var engine = switcher.CreateDefaultEngine();
            return engine.Evaluate<int>("1 + 1") == 2;
        }
        catch (JsEngineLoadException)
        {
            return false;
        }
    }
}
