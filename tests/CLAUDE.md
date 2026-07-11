# Tests

## Running tests

- Tests use **xUnit v3** (pre-release `xunit.v3`, Microsoft.Testing.Platform), FluentAssertions, and Moq — not xUnit v2 idioms.
- `dotnet test` does **not** work; run a test project directly: `dotnet run --project tests/SimpleCrawler.Tests -c Release`.
- Filter with the MTP native runner (note the **single** dash — `--filter` errors with `unknown option`):
  - By class: `dotnet run --project tests/SimpleCrawler.Tests -c Release -- -class "*ClassName"`
  - By method: `... -- -method "*.MethodName"` (wildcards allowed at either end)
  - By namespace: `... -- -namespace "SimpleCrawler.Tests"`
  - Query form: `... -- -filter "/assemblyName/namespace/class/method"` (see xUnit query-filter language)

## Benchmarks

- `SimpleCrawler.Benchmarks` is a runnable BenchmarkDotNet console app: `dotnet run -c Release --project tests/SimpleCrawler.Benchmarks`. Always run benchmarks in Release.
- Don't re-run the slow, noisy SPA render benchmarks just to prove a change is perf-neutral — lean on the correctness tests instead.

## ProfileRunner (JS-render profiling harness)

`SimpleCrawler.ProfileRunner` is a separate console app (CommandLineParser verbs, not a benchmark). It drives the real crawl/render path against a test-host SPA:

- `dotnet run --project tests/SimpleCrawler.ProfileRunner -- profile <combo> <iterations> [framework]` — crawls repeatedly so `RenderProfiler` with `JSRENDER_PROFILE=1` prints a per-phase table.
- `dotnet run --project tests/SimpleCrawler.ProfileRunner -- rendersize <combo> [framework]` — renders once, dumps element/anchor counts + serialized HTML. This is the fastest way to inspect what a JS-rendered page actually looks like.
- `combo` = `jint|v8`; `framework` = `react|preact|vue|svelte|solid`. Pass `--help` or `profile --help` for defaults.

## TestHost

- `SimpleCrawler.TestHost` serves embedded resources as static files so integration test servers run entirely from memory.
- The site is an Astro app building five client-only SPAs (react/preact/vue/svelte/solid), consumed by the JS-render and headless tests via per-framework `SpaHostFixture`s. After changing its sources, rebuild with `npm run build` (astro build) in `tests/SimpleCrawler.TestHost` so the embedded output updates.
- Embedded resources rely on the csproj `LogicalName` mapping — new files must land under the paths that mapping covers, or the in-memory server won't see them.

## Code style

Test projects don't generate XML docs and aren't referenced by consumers, so the root XmlDoc-for-declarations rule is applied minimally here: only type-level (class/enum) summary comments become `/// <summary>`; method, field, and in-body comments stay plain `//`.
