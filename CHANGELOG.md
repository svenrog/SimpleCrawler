# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Package versions are derived from git tags (`v*`) via MinVer.

Entries before 4.0.0 are condensed to what changed; the reasoning behind each is in the commit that made it.

## [Unreleased]

## [4.1.0] - 2026-08-17

### Added

- The DOM prelude gained the browser APIs a survey of production pages found it missing, each one an
  exception inside a real bundle's init rather than a missed lookup: `document.URL`/`documentURI` and
  `Node.baseURI`; `performance.timing` with its `navigation` counterpart from
  `getEntriesByType("navigation")`; `document.createTreeWalker`/`createNodeIterator` and the `NodeFilter`
  constants they are called with; `document.write`/`writeln` (appending, never the browser's post-load
  document-clearing behaviour) with `open`/`close` beside them; `Text.splitText`; and the `DOMTokenList`,
  `Window` and `File` constructors.
- `JsPageTimeoutException`, a `TimeoutException` named for the scope it bounds: a ceiling over the whole page
  has to end the render, while one over a single execution call must not.

### Changed

- `FormData`, `Headers`, `Request` and `Response` moved out from behind `EnableFetch` into the base prelude.
  They construct and hold data and issue nothing; a caller declines the fetch shim to keep the page's
  requests off the network, not to lose four constructors a form widget builds its payload in. Only
  `fetch`/`XMLHttpRequest`/`__http` remain gated, and the shim still reinstalls its own copies so a fetched
  response and the global a page tests it against stay the same class.
- `classList` returns a real `DOMTokenList` instance, one per element, instead of a fresh object literal per
  read — so identity tests and `DOMTokenList.prototype` patches both work. The token operations are unchanged.

### Fixed

- One isolation policy at every crossing into the JS engine, stated once instead of per call site:
  cancellation and a page-scoped ceiling propagate, everything else is a counted warning and the render
  continues. A raw CLR exception — most often Jint's per-script `TimeoutException`, but equally anything host
  code the engine called threw — used to escape from any of a dozen unprotected crossings (the preludes, the
  location/viewport setup, the script collection, the drain loop, and the finalize that reads the settled
  tree) and discard a render that had already run. A page is still abandoned once it has spent several script
  ceilings, which is what bounds the total.
- A `<script type="module">` appended at runtime runs through the module loader instead of the classic-script
  entry, so its imports resolve. The initial markup was already split by type; the runtime path never was.
- Two inline `<script type="module">` blocks on one page both run. They shared a specifier — the page URL —
  which Jint refuses outright (taking both) and V8 answers from its module cache (running the first twice).
- `document.getElementsByTagName`/`getElementsByClassName`/`getElementsByName` include the root element,
  which they searched strictly below. jQuery resolves a tag-only `$("html")` through the first of those, so
  a CMS bundle reading `$("html").attr("lang")` at init got undefined and threw. An *element's* own search
  still excludes itself, as it should.

## [4.0.0] - 2026-07-31

### Added

- Every JS engine now carries a wall-clock ceiling on how long page scripts may run, on the engine's own
  options and named for what that engine can measure it over: `JintEngineOptions.ScriptTimeout` bounds each
  execution call, since Jint checks its constraints between statements and restarts the timer whenever the
  engine is re-entered; `V8EngineOptions.PageTimeout` bounds the page, since V8 exposes no such hook and is
  interrupted from another thread. Both default to 30 seconds and raise `TimeoutException`; `TimeSpan.Zero`
  removes the ceiling. One shared setting would have had to be read as whichever the registered engine
  happened to mean, so there are two names rather than one.
- `SeededOptionsFactory<TOptions>` and `AddSeededOptions<TOptions>`, which is how an `AddXyzJsEngine(options)`
  overload now registers the instance it was handed.

### Changed

- **Breaking:** `IJsEngineFactory.Create` takes the caller's `CancellationToken`. Page scripts run
  synchronously on the calling thread, so a token observed only at the renderer's `await` points reaches a
  running page never — an engine has to be handed it and stop itself. Breaking for custom `IJsEngineFactory`
  implementers; a consumer that only resolves a factory and passes it to `JsRenderer` is unaffected.
- `AddJintJsEngine` takes an optional `JintEngineOptions`, matching `AddV8JsEngine`. Source-compatible; a
  caller compiled against 3.x needs a rebuild.
- A cancelled render is reported as `OperationCanceledException` by every engine rather than in that engine's
  own vocabulary. Jint's `ExecutionCanceledException` derives from its own base and V8's
  `ScriptInterruptedException` from `ScriptEngineException`, so either escaping untranslated is swallowed by
  a caller's "any exception means this render failed" handler — leaving the run to carry on with the
  cancellation having reached nothing.

### Fixed

- A page that never returns no longer runs until the process does. A live page hit two gaps at once: a
  function iterating with `for-of` and calling itself, adding about seven frames a minute while doing
  exponentially more work per level. It pegged a core indefinitely at a depth `MaxExecutionStackCount` would
  have taken hours to reach — that guard bounds recursion, not time — and nothing could ask it to stop, since
  no cancellation reached the engine. Either limit now ends it.
- An options instance passed to `AddJintJsEngine`/`AddV8JsEngine`/`AddV8Crawler` no longer blocks the caller's
  own `services.Configure<T>(…)`. It was registered as a closed `IOptions<T>`, which wins over the open
  generic, so a later `Configure` bound to nothing: the settings never arrived, the defaults stayed, and
  nothing said so. The instance now seeds `OptionsFactory<T>.CreateInstance`, so every registered `Configure`
  runs over it — a setting made that way wins, and one the caller never touched keeps the value it was given.
  On 3.x this silently affected `MaxHeapSizeMb`, `MaxUsesPerRuntime` and `HeapSampleInterval`.

## [3.6.5] - 2026-07-30

### Fixed

- `element.attributes` answers a lookup by name, so a jQuery-era library survives its own feature detection.
  jQuery 1.9–1.12 read `div.attributes[name].expando` during init and dereferenced `undefined`, throwing
  before `window.jQuery` was assigned — the library lost, and every plugin loaded over it with it.
- A `<script src>` the host asked for and was refused is now reported. A non-success status yielded no source
  and fired the node's error event without a warning, and the module half answered one with an empty module in
  silence, so a partial render was indistinguishable from a page that never carried the code. The module
  fetcher takes the logger it never had.

## [3.6.4] - 2026-07-29

### Fixed

- An unparseable `<script src>` no longer costs the page every script after it on Jint. External scripts are
  prepared outside the engine, so a parse failure arrived as a host `ScriptPreparationException` rather than
  the JS `SyntaxError` the per-script isolation catches; it is now raised as the script error it is, for
  scripts and modules alike.
- A `<script src>` the host cannot fetch fires the node's error event instead of aborting the render — a
  `blob:` URL, a faulting request, a timeout. A non-HTTP scheme is no longer requested at all.
- Two DOM-prelude gaps, each a bare `ReferenceError` during init that cost every global its bundle would have
  set: `Storage` is a global constructor, and `fetch` is present as an inert stub that rejects the way a
  browser rejects a refused request. `EnableFetch` still governs whether a request is made.

## [3.6.3] - 2026-07-29

### Fixed

- Runaway recursion in page code no longer takes the process down on Jint. Interpreted JS runs on the CLR
  stack, so it was an uncatchable `StackOverflowException`; `MaxExecutionStackCount` (2000 frames) raises the
  JS `RangeError` a browser raises instead, costing the script rather than the crawl. `LimitRecursion` and
  `LimitMemory` are declined in code, each with its reason.

## [3.6.2] - 2026-07-29

### Fixed

- Jint `Function.prototype.toString()` returns the source of a function an **external** script defined rather
  than a `[native code]` placeholder — prepared scripts did not inherit the engine's
  `RetainFunctionSourceText`, so every bundle fetched over the wire lost its own source.

## [3.6.1] - 2026-07-29

### Added

- `navigator.appVersion` and `navigator.plugins` to the DOM prelude. Browser sniffers index both unguarded, so
  an absent one was a `TypeError` that aborted the chunk carrying it.

## [3.6.0] - 2026-07-28

### Changed

- Raised the `Jint` dependency floor to `4.15.0` (from `4.12.0`); the range stays open across 4.x
  (`[4.15.0,5)`), so a consumer holding an earlier 4.x resolves upward.

## [3.5.2] - 2026-07-18

### Added

- `XMLSerializer`, the inverse of 3.5.1's `DOMParser`, delegating to the serializer behind `Element.outerHTML`.
- `window.postMessage`, delivering through the task queue to both the handler property and `message` listeners.

## [3.5.1] - 2026-07-18

### Added

- `DOMParser` with a working `parseFromString`. `text/html` nests the result under `html`/`head`/`body`;
  `xml`/`svg` keeps the parsed root as `documentElement`. Never throws on malformed input.

## [3.5.0] - 2026-07-16

### Added

- `PromiseRejectionEvent` as a callable global — inert, but its absence sent core-js down its
  replace-the-native-`Promise` path and broke `finally`.
- `DOMRect`/`DOMRectReadOnly`, `OffscreenCanvas`, `Node.prototype.compareDocumentPosition` with the
  `DOCUMENT_POSITION_*` constants.
- `AbortController`/`AbortSignal` and an inert `XMLHttpRequest` in the base prelude, rather than only
  alongside the fetch shim.

### Fixed

- `in` on a style declaration answers for every CSS property, matching what the property read already claimed.
- `lang` is reflected on `Element` as a string rather than reading `undefined`.
- `Element.animate()` returns an Animation carrying `finished`/`ready` plus
  `commitStyles`/`persist`/`updatePlaybackRate`.

## [3.4.0] - 2026-07-16

### Fixed

- `<meta>` reflects `content`/`name`/`http-equiv` as properties. There was no `HTMLMetaElement`, so bootstrap
  data parked in a meta tag read `undefined` and `JSON.parse` threw during render.
- `window.frames`/`top`/`parent` are the window itself and `window.length` is 0, matching a top-level context
  with no child frames — the IAB TCF consent stub indexes `frames` unguarded.

## [3.3.3] - 2026-07-16

### Added

- `PerformanceObserver`, `Worker` and `navigator.sendBeacon` on the rendered window; each is reached bare while
  an analytics SDK installs itself. `WebSocket`, `Notification`, `navigator.connection` and
  `navigator.serviceWorker` were considered and declined in code.

### Fixed

- A script's `src` is reflected as a URL attribute again: `getAttribute("src")` returns the authored string
  while `.src` resolves against the document base. Collapsing the two broke Turbopack's chunk identity.
- `XMLHttpRequest` completion callbacks fire off the microtask queue instead of inline inside `send()`.
- `Element.prototype.scrollTo`/`scrollBy`/`scroll`/`scrollIntoView` exist as no-ops, mirroring the window half.

## [3.3.2] - 2026-07-15

### Added

- Opt-in execution of runtime-injected cross-origin scripts (`JsRenderOptions.ExecuteCrossOriginScripts`, off
  by default). Off stays right for crawling; a render observing what a page *installs* needs them.

## [3.3.1] - 2026-07-15

### Added

- `JsRenderer.CollectAsync`: renders a shell and returns only the registered `IRenderedDomCollector` slices —
  `ExtractAsync`'s surface for a consumer that renders without crawling.
- Engine-only DI registrations `AddV8JsEngine`/`AddJintJsEngine`, so driving `JsRenderer` no longer means
  standing up a crawl pipeline to reach a factory behind an internal key.

## [3.3.0] - 2026-07-13

### Added

- Max crawl depth (`CrawlerOptions.MaxDepth`, `--maxDepth`), surfaced per URL on `UrlReport.Depth`.
- URL normalization before de-duplication (`NormalizeUrls`, `--normalizeUrls`, on by default).
- Include/exclude link filters (`--include`/`--exclude`) over a new `SimpleCrawler.Core.Filtering.UrlFilter`,
  reusing the robots matcher's longest-match/allow-wins resolution.
- Opt-in per-page signal capture (`CapturePageSignals`, `--captureSignals`) onto `UrlReport.Signals`.
- Pluggable per-page collectors (`SimpleCrawler.Core.Collectors`): `ICrawlCollector`/`IDomCollector`, expressed
  once per backend family and registered by `AddCrawlCollectors`.

### Changed

- Updated `Jint` to `4.12.0`.
- Checkpoint format: only the pending frontier with per-URL depth is persisted, rebuilding `Discovered` on
  load. Checkpoints written by earlier versions are not resumed.

### Security

- Narrowed V8 document access from `EnableAllLoading` to `EnableWebLoading`, removing a latent local-file-read
  path an `import()` could resolve toward.

## [3.2.0] - 2026-07-11

### Added

- Opt-in WebGL stub (`JsRenderOptions.EnableWebGl`, `--webgl`), off by default: map/3D libraries throw on a
  null context and take the page's links with them.

## [3.1.0] - 2026-07-11

### Added

- Live crawl-time ETA, on by default (`SimpleCrawler.Core.Progress`; `--progress`, `--progressInterval`,
  `--progressConfirm`).

### Changed

- Replaced the shared FIFO frontier with a host-partitioned one
  (`SimpleCrawler.Core.Scheduling.HostFrontier`), ending cross-site head-of-line blocking.
- Updated `Simple.Logging.Console` to 2.0.0; the CLI uses truecolor `AddRgbConsoleLogging()`.
- Normalized per-page log lines and proxy references across backends; checkpointing logs its activity and
  `ICheckpointStore` gained `Target`.

### Fixed

- JS DOM: `IntersectionObserverEntry` as a global, `<canvas>` as a real `HTMLCanvasElement` with a no-op 2D
  context, and `getComputedStyle` returning a readable `CSSStyleDeclaration`.

## [3.0.0] - 2026-07-09

### Added

- Custom request headers (`-H`/`--header`, repeatable) merged into the browser profile; `--cookie` follows the
  same path.
- Adaptive per-host throttling, on by default (`CrawlerOptions.Throttling`, `--adaptiveThrottle`).
- Checkpoint/resume (`--checkpoint`), with new `Checkpoints` and `Throttling` namespaces.
- Per-URL reporting (`UrlReport`/`CrawlOutcome` on `IScrapeResult.Reports`, optional `--report`).
- Opt-in WHATWG Streams for the JS backends (`JsRenderOptions.EnableStreams`, off by default).
- JS renderer exception diagnostics on an unconditional `__crawlerDiagnostic` channel at `Debug`.
- Cross-page render fetch cache for `EnableFetch`, honouring `Cache-Control`/`Pragma`/`Vary`.
- Next.js RSC prefetches short-circuited with a `204`.

### Changed

- Per-host throttling extracted into `AdaptiveThrottler`, reworked to a lock-free next-slot reservation.
- **Breaking:** `IJsEngine` no longer requires `IDisposable`; engines own their teardown and `JsRenderer`
  disposes them on every exit path.
- Updated `PuppeteerSharp` to >= 25.0.4.

### Removed

- CLI `--proxyRetries` (deprecation period over).
- **Breaking:** the dead bridge-era `IJsEngine` members `GetGlobalObject`, `CreateArray`, `InvokeCallback`,
  `EmbedHostType`, and the unreachable `__crawlerReset` prelude machinery.
- The Jint `Map.keys()`/`values()` iterator compat shim, fixed upstream in Jint 4.11.

### Fixed

- robots-meta parsing defaulted `index`/`follow` to `false`, so a lone `content="index"` dropped every link.
- RSC-adjacent DOM shim gaps that crashed Next.js App Router hydration (live `NamedNodeMap`, the attribute-node
  methods, `getRootNode`, `getElementsByName`, `reportError`, `readyState`/`DOMContentLoaded`).
- Checkpoint/resume was unreachable for the AngleSharp/JS/Playwright/Puppeteer backends.
- `Ctrl+C` raced the checkpoint save.
- JS DOM: no-op Constraint Validation API, `FileList`, a `CSSTransition` shim, null-returning `getComputedStyle`.
- Jint `Function.prototype.toString()` returned `"[native code]"` for all functions, breaking jQuery/Sizzle's
  native-code sniff.

## [2.0.0] - 2026-07-07

Public proxy types are removed and renamed (see _Removed_ and _Changed_), a breaking change under SemVer.

### Added

- Generalized retry for every backend, with or without a proxy pool: connection errors, timeouts, `429` and
  `5xx` retried with exponential backoff and jitter.
- `RetryOptions` on `CrawlerOptions.Retry`, with a per-attempt timeout; CLI `--retries`, `--retryDelay`,
  `--maxRetryDelay`, `--attemptTimeout`.
- New `SimpleCrawler.Core.Retry` namespace, and `AbstractHeadlessCrawler<TPage, TResult>` owning the shared
  page pool and acquire/retry loop.

### Changed

- Proxy rotation is the "alternative route" case of the shared retry loop.
- **Breaking:** `IProxyPool.ReportFailure` takes `RetryReason` instead of `ProxyFailureKind`.
- Retry is installed at the HTTP layer, so robots/sitemap probes and JS sub-resource fetches inherit it.
- `PlaywrightCrawler<TResult>`/`PuppeteerCrawler<TResult>` derive from `AbstractHeadlessCrawler`;
  source-compatible.

### Removed

- **Breaking:** `ProxyFailureKind`, `ProxyAttempt<T>`, `ProxyFailureClassifier`, `ProxyRetryExecutor` and
  `ProxyRoutingHandler`, superseded by the `Retry` namespace.
- **Breaking:** `ProxyPoolOptions.MaxRetries`; the retry budget moved to `RetryOptions.MaxRetries`.

### Compatibility

- The CLI stays backward-compatible: `--proxyRetries` is a hidden alias for `--retries`.

## [1.0.0] - 2026-07-07

- Initial release.

[Unreleased]: https://github.com/svenrog/SimpleCrawler/compare/v4.1.0...HEAD
[4.1.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v4.1.0
[4.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v4.0.0
[3.6.5]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.6.5
[3.6.4]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.6.4
[3.6.3]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.6.3
[3.6.2]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.6.2
[3.6.1]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.6.1
[3.6.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.6.0
[3.5.2]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.5.2
[3.5.1]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.5.1
[3.5.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.5.0
[3.4.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.4.0
[3.3.3]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.3.3
[3.3.2]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.3.2
[3.3.1]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.3.1
[3.3.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.3.0
[3.2.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.2.0
[3.1.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.1.0
[3.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.0.0
[2.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v2.0.0
[1.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v1.0.0
