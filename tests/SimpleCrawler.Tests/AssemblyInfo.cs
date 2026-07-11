using System.Runtime.InteropServices;

[assembly: ComVisible(false)]
[assembly: Guid("e39cfc02-ae98-49b2-a9c6-1651617745b1")]

// Parallelization is deliberately NOT disabled assembly-wide, but every resource-heavy class shares
// [Collection("Crawler")] so they run serially relative to each other - never piling up concurrent
// headless browsers or V8 isolate pools, which would exhaust a 2-core GitHub Actions runner (OOM / CPU
// contention -> flaky timeouts). That collection covers both the host/port-binding crawler tests (fixed
// ports 5260-5287, plus the process-global ASPNETCORE_URLS a couple of fixtures set) and the port-less
// JS-engine tests (JsDomRendererTests, JsModuleFetchTests, and the two engine benches). Only the cheap
// pure-unit classes parallelize. If CI still shows browser flakiness, cap maxParallelThreads in
// xunit.runner.json rather than re-serializing everything.