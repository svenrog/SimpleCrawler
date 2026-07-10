using Jint;
using Jint.Runtime;

namespace SimpleCrawler.Tests;

/// <summary>
/// Documents (as living tests) why Vue renders on V8 but is skipped on Jint: Jint mis-evaluates Vue's
/// runtime-core when run as an ES module — it throws "Cannot access 't' before initialization" — yet the
/// identical source runs fine as a plain script and on V8. So it is a Jint module-mode evaluation bug, not a
/// pure-JS DOM gap or a fault in our loader/cache/prelude (this harness uses none of them). If the module
/// case ever stops throwing (a Jint fix), revisit JsModeSpaCrawlerTests' vue/Jint skip. See
/// [[phase5-js-spa-hydration]].
/// </summary>
public class JintModuleTdzRepro
{
    // The bundled Vue runtime-core (reactivity inlined) only exists after the Astro TestHost SPA is built
    // locally; it lives under a gitignored build/node_modules tree and is absent on CI, so these tests skip
    // there. Search is bounded at the repo root and ignores inaccessible directories so it never crawls (or
    // trips over the permissions of) anything outside the checkout.
    private static string? RuntimeCoreSource()
    {
        var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var hit = dir.GetFiles("runtime-core.esm-bundler.*.js", options);
            if (hit.Length > 0) return File.ReadAllText(hit[0].FullName);
            if (dir.GetFiles("SimpleCrawler.slnx").Length > 0) return null;
            dir = dir.Parent;
        }
        return null;
    }

    [Fact]
    public void RuntimeCore_AsModule_ThrowsTdzOnJint()
    {
        var source = RuntimeCoreSource();
        Assert.SkipWhen(source is null, "Bundled Vue runtime-core not built (CI / no local SPA build).");

        var engine = new Engine(o => o.EnableModules(AppContext.BaseDirectory));
        engine.Modules.Add("rc", b => b.AddModule(Engine.PrepareModule(source!, "rc")));

        var ex = Assert.Throws<JavaScriptException>(() => engine.Modules.Import("rc"));
        Assert.Contains("before initialization", ex.Message);
    }

    [Fact]
    public void RuntimeCore_AsScript_EvaluatesOnJint()
    {
        var source = RuntimeCoreSource();
        Assert.SkipWhen(source is null, "Bundled Vue runtime-core not built (CI / no local SPA build).");

        var scriptSource = System.Text.RegularExpressions.Regex.Replace(source!, @"export\{[^}]*\};?", "");

        var engine = new Engine();
        var ex = Record.Exception(() => engine.Execute(scriptSource));

        Assert.Null(ex);
    }
}
