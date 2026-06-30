# JavaScript crawlers

Using `Crawler.Js` crawlers requires 2 choices (engine + parser), plus render options.

## Engine

### Jint (`Crawler.Js.Jint`)

- No native dependencies.
- All memory on the managed GC heap, profiler sees the whole footprint.
- Slower and more allocation-heavy than V8 (interpreter vs JIT).
- Can't crawl Vue-driven sites due to a problem with ESM loading.

Use for AOT, locked-down environments or just don't like dependencies.

### V8 (`Crawler.Js.V8`)

- Much faster on render-heavy pages (optimising JIT).
- Broadest compatibility, including Vue.
- Lowest *managed* allocations.
- **Memory lives outside the .NET heap.** The V8 isolate has its own native heap, so managed profilers
  understate real footprint, the low "Allocated" numbers in [performance](./performance.md) are misleading.
  Size host RAM for it, especially at high `ParseConcurrency`.
- Ships a version-locked, per-platform native binary. AOT works but deployment is heavier than Jint.

Use for throughput and max compatibility, when you can budget the off-heap memory.

## HTML parser

The HTML must become a DOM tree before scripts run. Native C# pre-parsing is far less resource intensive than the JS tokenizer.

| Registration                     | Shell parsed by                  | Speed           |
| -------------------------------- | -------------------------------- | --------------- |
| *(none)*                         | `dom.js` JS tokenizer            | slowest         |
| `AddAngleSharpHtmlParser()`      | AngleSharp (spec-compliant)      | middle          |
| `AddHtmlAgilityPackHtmlParser()` | HtmlAgilityPack                  | **fastest**     |

Register HtmlAgilityPack unless you need AngleSharp's spec-compliant handling of pathological markup.

## JsRenderOptions

Passed to `AddJintCrawler`/`AddV8Crawler`.

| Option | Default | Effect |
| ------ | ------- | ------ |
| `EnableFetch` | `false` | Enables real network `fetch`/`XHR` for runtime-loaded content/links to render. |
| `Viewport` | 1920×1080 | Window/screen reported to scripts. Set a mobile screen size to crawl the mobile layout on responsive sites. |
| `ScriptLogging` | `null` | `LogLevel` floor for forwarding page `console.*` to your logger. `Debug` to diagnose non-rendering pages. |
| `MaxTaskDrainIterations` | `1000` | Cap on microtask/chunk-load drain iterations before giving up on a page. |
