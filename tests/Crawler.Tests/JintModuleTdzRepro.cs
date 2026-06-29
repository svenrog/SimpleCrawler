using Jint;
using Jint.Runtime;

namespace Crawler.Tests;

// Documents (as living tests) why Vue renders on V8 but is skipped on Jint: Jint mis-evaluates Vue's
// runtime-core when run as an ES module — it throws "Cannot access 't' before initialization" — yet the
// identical source runs fine as a plain script and on V8. So it is a Jint module-mode evaluation bug, not a
// pure-JS DOM gap or a fault in our loader/cache/prelude (this harness uses none of them). If the module
// case ever stops throwing (a Jint fix), revisit JsModeSpaCrawlerTests' vue/Jint skip. See
// [[phase5-js-spa-hydration]].
public class JintModuleTdzRepro
{
    private static string RuntimeCoreSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var hit = dir.GetFiles("runtime-core.esm-bundler.*.js", SearchOption.AllDirectories);
            if (hit.Length > 0) return File.ReadAllText(hit[0].FullName);
            dir = dir.Parent;
        }
        throw new FileNotFoundException("runtime-core.esm-bundler.*.js");
    }

    [Fact]
    public void RuntimeCore_AsModule_ThrowsTdzOnJint()
    {
        var source = RuntimeCoreSource();
        var engine = new Engine(o => o.EnableModules(AppContext.BaseDirectory));
        engine.Modules.Add("rc", b => b.AddModule(Engine.PrepareModule(source, "rc")));

        var ex = Assert.Throws<JavaScriptException>(() => engine.Modules.Import("rc"));
        Assert.Contains("before initialization", ex.Message);
    }

    [Fact]
    public void RuntimeCore_AsScript_EvaluatesOnJint()
    {
        var source = RuntimeCoreSource();
        var scriptSource = System.Text.RegularExpressions.Regex.Replace(source, @"export\{[^}]*\};?", "");

        var engine = new Engine();
        var ex = Record.Exception(() => engine.Execute(scriptSource));

        Assert.Null(ex);
    }
}
