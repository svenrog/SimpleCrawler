# JavaScript crawlers

Since AngleSharp or any other library lacked the ability to render simpler JavaScript sites, the `SimpleCrawler.Js` project that provides a rendering engine was added. There are 2 engine implementations, `Jint` and `ClearScript.V8`.

Both are tested against the client-side frameworks and libraries below. The test do not cover the full featureset, a list of real sites have been used as a reference.

- [React](https://react.dev/) 
- [Preact](https://preactjs.com/)
- [Solid](https://www.solidjs.com/)
- [Svelte](https://svelte.dev/)
- [Vue](https://vuejs.org/) (Jint can't run [Vue](./docs/javascript-crawlers.md))

Using `SimpleCrawler.Js` crawlers requires choosing an engine (`Jint` or `V8`).

## Engine

### Jint (`SimpleCrawler.Js.Jint`)

- No native dependencies.
- All memory on the managed GC heap, profiler sees the whole footprint.
- Slower and more allocation-heavy than V8 (interpreter vs JIT).
- Can't crawl Vue-driven sites due to a problem with ESM loading.

Use if memory management is needed or dependencies can't be installed.

### V8 (`SimpleCrawler.Js.V8`)

- Much faster on render-heavy pages (optimising JIT).
- Broadest compatibility.
- **Memory lives outside the .NET heap.** The V8 isolate has its own native heap, so managed profilers understate real footprint, the low "Allocated" numbers in [performance](./performance.md) are misleading. Size host RAM for it, especially at high `ParseConcurrency`.
- Ships a version-locked, per-platform native binary.

## JsRenderOptions

Passed to `AddJintCrawler`/`AddV8Crawler`.

| Option | Default | Effect |
| ------ | ------- | ------ |
| `EnableFetch` | `false` | Enables real network `fetch`/`XHR` for runtime-loaded content/links to render. |
| `EnableIndexedDb` | `false` | Installs an in-memory `indexedDB`. Turn it on for SPA sites that rely on offline features. |
| `EnableStreams` | `false` | Installs a [WHATWG Streams](https://streams.spec.whatwg.org/) surface. Delivering spec-compliant reader/transform semantics, not incremental transport streaming. See [RSC and streaming bundles](#rsc-and-streaming-bundles). |
| `EnableWebGl` | `false` | Makes `canvas.getContext("webgl"/"webgl2")` return a non-faulting stub instead of `null`. Turn it on for map/3D sites (Mapbox GL, Three.js, deck.gl) that otherwise throw "Failed to initialize WebGL." and trip their error boundary, losing the page's links. The map itself doesn't render (it yields no anchors), but the surrounding page does. |
| `Viewport` | 1920×1080 | Window/screen reported to scripts. Set a mobile screen size to crawl the mobile layout on responsive sites. |
| `ScriptLogging` | `null` | `LogLevel` floor for forwarding page `console.*` to your logger. `Debug` to diagnose non-rendering pages. |
| `MaxTaskDrainIterations` | `1000` | Cap on microtask/chunk-load drain iterations before giving up on a page. |

## RSC and streaming bundles

Next.js App Router (React Server Components) sites render on the **V8** engine.

**Jint** used to fail on the same sites with "Cannot convert undefined or null to object" from React's Flight deserialization. That was an interpreter bug — an unlabeled `break` escaped a labeled `switch`, so a function fell off its end and returned `undefined` where Flight expected an object ([jint#2607](https://github.com/sebastienros/jint/pull/2607)). It was fixed upstream and shipped in Jint 4.12.0; since SimpleCrawler 3.6.0 this backend requires 4.15.0 or newer, so that failure is gone. RSC sites aren't part of the offline test suite, so V8 (the default) is still the safer choice for them — and the faster one on pages that heavy.

`EnableStreams` delivers a buffered-complete body (the fetch already materialises the whole response), so consumers get spec-compliant reader/transform semantics, not chunks-over-time, and the baseline guard keeps the server markup intact if the
streaming path would otherwise tear it down. Sites that need real streaming should use the [Playwright](./configuration.md) or [Puppeteer](./configuration.md) headless backends.

## Engine reuse

The two engines handle per-page setup differently.

- **V8** pools the *isolate* (its native heap and compilation cache) sized to the crawl's `Concurrency` — set `Concurrency` to 8 and the pool holds up to 8 isolates. An isolate is rented per page and returned when the page finishes, so isolate spin-up is amortised across the crawl. Every page still renders in a fresh context, so per-page globals are always clean. Because a pooled isolate's native heap grows with every distinct script it compiles, each isolate is retired and rebuilt after `MaxUsesPerRuntime` pages.
- **Jint** builds a **fresh engine per page** and disposes it when the page finishes. On current Jint this is deliberate: `new Engine()` is a negligible cost. See [performance](./performance.md).

### V8EngineOptions

| Option | Default | Effect |
| ------ | ------- | ------ |
| `MaxHeapSizeMb` | `256` | Sets the max heap allocation of each isolate. This value should be multiplied by the number of threads (by default 2048 MiB) |
| `MaxUsesPerRuntime` | `50` | The maximum number of uses for each engine, the heap will grow uncontrollably during crawl (depending on client scripts encountered) and this can be used as a safety valve |
| `HeapSampleInterval` | `250&nbsp;ms` | The interval at which to sample memory use, lower values use more system resources. |