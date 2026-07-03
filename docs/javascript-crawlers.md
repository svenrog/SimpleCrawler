# JavaScript crawlers

Since AngleSharp or any other library lacked the ability to render simpler JavaScript sites we added the `Crawler.Js` project that provides a rendering engine. There are 2 engine implementations, `Jint` and `ClearScript.V8`.

Both are tested against the client-side frameworks and libraries below. The test do not cover the full featureset, a list of real sites have been used as a reference.

- [React](https://react.dev/) 
- [Preact](https://preactjs.com/)
- [Solid](https://www.solidjs.com/)
- [Svelte](https://svelte.dev/)
- [Vue](https://vuejs.org/) (Jint can't run [Vue](./docs/javascript-crawlers.md))

Using `Crawler.Js` crawlers requires 2 choices (engine and parser).

## Engine

### Jint (`Crawler.Js.Jint`)

- No native dependencies.
- All memory on the managed GC heap, profiler sees the whole footprint.
- Slower and more allocation-heavy than V8 (interpreter vs JIT).
- Can't crawl Vue-driven sites due to a problem with ESM loading.

Use if memory management is needed or dependencies can't be installed.

### V8 (`Crawler.Js.V8`)

- Much faster on render-heavy pages (optimising JIT).
- Broadest compatibility.
- **Memory lives outside the .NET heap.** The V8 isolate has its own native heap, so managed profilers understate real footprint, the low "Allocated" numbers in [performance](./performance.md) are misleading. Size host RAM for it, especially at high `ParseConcurrency`.
- Ships a version-locked, per-platform native binary.

## HTML parser

The HTML must become a DOM tree before scripts run. Native C# pre-parsing is far less resource intensive than the JS tokenizer.

| Registration                     | Shell parsed by                  | Speed           |
| -------------------------------- | -------------------------------- | --------------- |
| *(none)*                         | `dom.js` JS tokenizer            | slowest         |
| `AddAngleSharpHtmlParser()`      | AngleSharp (spec-compliant)      | middle          |
| `AddHtmlAgilityPackHtmlParser()` | HtmlAgilityPack                  | **fastest**     |

HtmlAgilityPack is preferred unless you need spec-compliant parsing.

## JsRenderOptions

Passed to `AddJintCrawler`/`AddV8Crawler`.

| Option | Default | Effect |
| ------ | ------- | ------ |
| `EnableFetch` | `false` | Enables real network `fetch`/`XHR` for runtime-loaded content/links to render. |
| `EnableIndexedDb` | `false` | Installs an in-memory `indexedDB`. Turn it on for SPA sites that rely on offline features. |
| `Viewport` | 1920×1080 | Window/screen reported to scripts. Set a mobile screen size to crawl the mobile layout on responsive sites. |
| `ScriptLogging` | `null` | `LogLevel` floor for forwarding page `console.*` to your logger. `Debug` to diagnose non-rendering pages. |
| `MaxTaskDrainIterations` | `1000` | Cap on microtask/chunk-load drain iterations before giving up on a page. |

## Engine pooling

Both engines keep a pool of render engines sized to the crawl's `Concurrency` — set `Concurrency` to 8 and the pool holds up to 8 engines. An engine is rented for each page and returned when the page finishes, so the cost of building an engine is amortised across the crawl instead of paid per page. What is pooled differs between the engines:

- **V8** pools the *isolate* (its native heap and compilation cache). Every page still renders in a fresh context, so per-page globals are always clean; the pool only amortises isolate spin-up.
- **Jint** has no isolate/context split, so it pools the whole *realm*: a reused engine keeps its globals and the installed DOM. Between pages it resets per-page state — the document, custom-element registry, timers, storage, and any globals a bundle added — instead of re-evaluating the ~90&nbsp;KB `dom.js` shim, which is the bulk of a Jint render's setup. This is what roughly halves Jint render time and allocation on setup-dominated pages ([performance](./performance.md)).

A pooled engine accumulates over its lifetime — V8's native heap grows with every distinct script it compiles, and a reused Jint realm can leak state past the reset — so each engine is retired and rebuilt after a fixed number of pages (`MaxUsesPerRuntime` / `MaxUsesPerEngine`).

### V8EngineOptions

| Option | Default | Effect |
| ------ | ------- | ------ |
| `MaxHeapSizeMb` | `256` | Sets the max heap allocation of each isolate. This value should be multiplied by the number of threads (by default 2048 MiB) |
| `MaxUsesPerRuntime` | `50` | The maximum number of uses for each engine, the heap will grow uncontrollably during crawl (depending on client scripts encountered) and this can be used as a safety valve |
| `HeapSampleInterval` | `250&nbsp;ms` | The interval at which to sample memory use, lower values use more system resources. |

### JintEngineOptions

| Option | Default | Effect |
| ------ | ------- | ------ |
| `MaxUsesPerEngine` | `50` | The number of pages an engine renders before it is disposed and rebuilt with a fresh realm. Bounds any per-page state that survives the between-page reset of a reused realm. `0` disables reuse (a fresh engine per page). |