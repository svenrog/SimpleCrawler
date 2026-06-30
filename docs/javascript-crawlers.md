# JavaScript crawlers

`Crawler.Js` Two required choices (engine + parser) plus render options. When to use this tier:
[overview](./configuration.md#tiers).

## Engine

### Jint (`Crawler.Js.Jint`)

- **NativeAOT-compatible** — managed code; publishes and runs under AOT (warnings only). The only JS engine that fits a self-contained AOT binary.
- No native dependencies.
- All memory on the managed GC heap, so a profiler sees the whole footprint.
- Slower and more allocation-heavy than V8 (interpreter vs JIT).
- Can't crawl Vue-driven sites due to a problem with ESM loading.

Use for AOT, zero deps, or locked-down environments.

### V8 (`Crawler.Js.V8`)

- Much faster on render-heavy SPAs (optimising JIT).
- Broadest compatibility, including Vue.
- Lowest *managed* allocations.
- **Memory lives outside the .NET heap.** The V8 isolate has its own native heap, so managed profilers
  understate real footprint — the low "Allocated" numbers in [performance](./performance.md) are misleading.
  Size host RAM for it, especially at high `ParseConcurrency`.
- Ships a version-locked, per-platform native binary. AOT works but deployment is heavier than Jint.

Use for throughput and max compatibility, when you can budget the off-heap memory.

## HTML parser

The shell HTML must become a DOM tree before scripts run. Native C# pre-parsing (handing `dom.js` a ready tree
via `__crawlerLoadTree`) is far cheaper than the JS tokenizer. Register at most one.

| Registration                     | Shell parsed by                  | Cost            |
| -------------------------------- | -------------------------------- | --------------- |
| *(none)*                         | `dom.js` JS tokenizer            | slowest         |
| `AddAngleSharpHtmlParser()`      | AngleSharp (spec-compliant)      | middle          |
| `AddHtmlAgilityPackHtmlParser()` | HtmlAgilityPack                  | **fastest**     |

Register HtmlAgilityPack unless you need AngleSharp's spec-compliant handling of pathological markup. Impact
compounds with engine choice: [performance](./performance.md).

## JsRenderOptions

`src/Crawler.Js/Models/JsRenderOptions.cs`. Passed to `AddJintCrawler`/`AddV8Crawler`.

| Option | Default | Effect |
| ------ | ------- | ------ |
| `EnableFetch` | `false` | Real network `fetch`/`XHR` so runtime-loaded content/links render. Costs live HTTP per page; required for data-driven SPAs. |
| `Viewport` | 1920×1080 | Window/screen reported to scripts (`innerWidth`, `matchMedia`, …). Set mobile to crawl the mobile layout. |
| `ScriptLogging` | `null` | `LogLevel` floor for forwarding page `console.*` to your logger. `Debug` to diagnose non-rendering SPAs. |
| `MaxTaskDrainIterations` | `1000` | Cap on microtask/chunk-load drain iterations before giving up on a page. |
