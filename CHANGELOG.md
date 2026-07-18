# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Package versions are derived from git tags (`v*`) via MinVer.

## [Unreleased]

## [3.5.1] - 2026-07-18

### Added

- `DOMParser`, with a working `parseFromString`. A bundle that parses a string of markup it fetched or built
  and then queries the result constructs `new DOMParser().parseFromString(html, "text/html")`; the missing
  global was a certain `ReferenceError` that failed the script before it could assign whatever it derived from
  the parse. `text/html` nests the result under `html`/`head`/`body` through the same document parser the shell
  uses, so `querySelector`/`getElementById` reach the parsed tree; `xml`/`svg` keeps the parsed root element as
  `documentElement`, since XML has no implied body. It never throws on malformed input, returning a near-empty
  document the way a browser hands back a `<parsererror>` one. Measured against a real browser this recovers
  a self-hosted Git forge's version global, read off a document it parses during init.

## [3.5.0] - 2026-07-16

### Added

- `PromiseRejectionEvent` as a callable global. It is inert — nothing dispatches it, because the renderer has
  no way to observe an unhandled rejection — but its *presence* is load-bearing, and its absence silently
  corrupted Promise semantics on any page carrying core-js. core-js decides the native `Promise` needs
  replacing when it looks like a browser but `window.PromiseRejectionEvent` is not callable; it then installs
  its own `Promise`, and a bundle that tree-shook the `es.promise.finally` add-on — reasonable, since `finally`
  is native everywhere it ships — gets a `Promise` without it. So `p.finally(...)` threw "not a function" deep
  in a hydration path that a real browser runs without complaint. Providing the global keeps core-js on its
  native-Promise path, exactly as it stays in Chrome.
- `DOMRect` and `DOMRectReadOnly`, constructible and deriving `top`/`right`/`bottom`/`left` from
  `x`/`y`/`width`/`height`, plus `fromRect`. Geometry code references them bare, so an absent global was a
  certain `ReferenceError`. They are not tied to layout: an unlaid-out element's rects stay zero-sized, as
  `getBoundingClientRect` already reports.
- `OffscreenCanvas`, wrapping the no-op 2d context `<canvas>` already returns. Graphics widgets composite
  off-screen with `new OffscreenCanvas(w, h).getContext("2d")`, sometimes from an effect during hydration, so
  the missing global failed the subtree. `transferToImageBitmap`/`convertToBlob` hand back inert results; this
  adds no drawing surface that was not already stubbed.
- `Node.prototype.compareDocumentPosition` and the `DOCUMENT_POSITION_*` constants. Focus and tab-order
  libraries sort nodes with an unguarded `a.compareDocumentPosition(b)` comparator, typically inside a memo
  during render, so the missing method threw, failed that subtree, and every effect below it silently never
  ran.
- `AbortController` and `AbortSignal` in the base prelude. They were installed only alongside the fetch shim,
  so under the default render an SDK constructing one during init hit a `ReferenceError`. They are general
  cancellation primitives with no network behaviour of their own and belong next to `crypto`/`TextEncoder`.
  Measured against a real browser this recovers an analytics SDK's two globals on one page and a 3D library's
  on another.
- An inert `XMLHttpRequest` in the base prelude — present and patchable, but `send()` issues no request and
  `readyState` never advances. `XMLHttpRequest` was absent by default while the rest of the shim surface is
  present-but-inert, so an SDK patching `XMLHttpRequest.prototype.open` unguarded at init threw where a
  no-op would have done. Enabling the fetch shim now overrides the stub with the functional implementation
  rather than deferring to it, so the network-quiet default is unchanged.

### Fixed

- `in` on a style declaration answers for every CSS property, matching what the property read already claimed.
  The style proxy had `get` and `set` traps but no `has`, so `"transform" in style` was false even for a
  property just assigned, while `style.transform` read back its value — the object contradicted itself. An
  animation library probes `"transform" in style || "WebkitTransform" in style || …` to pick a vendor prefix,
  found none, and used the resulting null as a property name, throwing on every transform read it made
  thereafter. The trap answers true for unknown names too, where a browser says false; feature probes ask
  about real properties, and the universal-`""` get trap already made that trade.
- `lang` is reflected on `Element` as a string. It read `undefined` where a browser returns the attribute or
  `""`, and since `lang` is a global attribute this reached any code doing `document.documentElement.lang`
  followed by a string method — routine in internationalization paths, on essentially every page that sets
  `<html lang>`. One consent platform's language lookup called `.replace()` on it, threw inside an un-awaited
  init, and left the whole consent manager parked: no lifecycle event, and every tag gated behind it unfired.
  With `lang` reflected, that manager's initialization matches a real browser's, down to its computed consent
  groups.
- `Element.animate()` returns an Animation carrying the Web Animations `finished` and `ready` promises, plus
  `commitStyles`/`persist`/`updatePlaybackRate`. The inert Animation covered the synchronous surface only, so
  sequencing code awaiting `.finished` before committing styles read `undefined.then` and threw. Both promises
  are already resolved, which is the truthful answer for an element that never animates.

## [3.4.0] - 2026-07-16

### Fixed

- `<meta>` reflects its `content` (and `name`/`http-equiv`) as a property. There was no `HTMLMetaElement` at
  all, so a meta tag parsed to a plain `Element` and `.content` read `undefined` — and a page's own bootstrap
  data is routinely parked in a meta tag and read back through the property, never the attribute
  (`JSON.parse(meta.content)`). That read is then `JSON.parse(undefined)`, a `SyntaxError` thrown during
  render, which a framework turns into a client-side exception and an error route: the container is emptied,
  hydration never commits, and so not one `useEffect` runs and nothing the page would have mounted appears.
  Every script fetched, every script executed, nothing surfaced — a Next.js page went from 1 hydrated node to
  1,389, and recovered its consent manager, its error tracker, its analytics and a utility library, all of
  which are injected from effects. Nothing caught this because the internal robots read goes through
  `getAttribute("content")` — the attribute, which worked.
- `window.frames`, `window.top` and `window.parent` are the window itself, and `window.length` is 0, matching
  a top-level browsing context with no child frames. The IAB TCF consent stub — shipped by essentially every
  consent platform — probes for an already-present CMP with a bare `window.frames['__tcfapiLocator']`, an
  indexed read rather than a feature test, so an absent `frames` was a `TypeError` that aborted the stub's
  entire script and took everything it would have installed with it. Measured against a real browser this
  recovers a live-chat SDK's global outright, and it clears that bundle-execution error on an unrelated page.

## [3.3.3] - 2026-07-16

### Added

- `PerformanceObserver`, `Worker` and `navigator.sendBeacon` on the rendered window. All three are reached
  while an analytics/tracing SDK installs itself, so a missing one threw a `ReferenceError` *through* that
  init and the SDK set none of the globals it would have — the page still rendered and nothing surfaced, so
  the technology simply read as absent. `PerformanceObserver` never fires its callback (a layout-less
  single-pass render produces no timing entries — the same reason `performance.getEntries()` is empty) but
  reports the usual `supportedEntryTypes`; `Worker` never runs the worker's script; `sendBeacon` reports
  success and sends nothing, which is what the default no-fetch posture already promises for a beacon.
  Measured against a real browser, `PerformanceObserver` alone recovers two technologies on a Next.js page
  whose tracing SDK carried a syntax highlighter into the same entry.
  `WebSocket`, `Notification`, `navigator.connection` and `navigator.serviceWorker` were considered and
  declined in code: `connection`/`serviceWorker` are feature-detected in practice, so a stub only diverts a
  page onto a branch it had deliberately skipped, and none of the four recovered a global on any sampled
  target. `Worker` is included on the opposite evidence — it is constructed bare, never guarded.

### Fixed

- A script's `src` is reflected as a URL attribute again, so `getAttribute("src")` returns the literal string
  the markup authored while the `.src` property returns it resolved against the document base. Both halves are
  read, and by different consumers: webpack's auto-public-path wants the resolved URL off `.src`, while
  Turbopack derives a chunk's identity by stripping its configured base path off `getAttribute("src")`
  (`path.startsWith(base) ? path.slice(base.length) : path`). Collapsing the two onto the resolved URL — the
  renderer handed `document.currentScript` the URL it had resolved to fetch, and the `src` setter reflected
  that into the attribute — failed Turbopack's prefix test, so every chunk registered under a key nothing
  awaited and the entry module's dependency gate never settled. A Next.js App Router page therefore never ran
  its entry module and never set `window.next`: every chunk fetched, every script executed, nothing thrown, no
  global defined. `HTMLScriptElement.src` now resolves in its getter (matching `HTMLAnchorElement.href`), and
  both paths that expose `document.currentScript` — initial-HTML scripts and runtime-appended chunks — pass the
  raw attribute.
- `XMLHttpRequest` completion callbacks (`readystatechange`/`load`/`loadend`) fire off the microtask queue
  instead of inline inside `send()`, matching a browser. Firing them synchronously ran a page's handler before
  the rest of the issuing script had executed, which breaks the ordinary shape of assigning what the handler
  depends on further down the same script than the request that triggers it — a consent stub whose geo `onload`
  called an instance method defined below its `send()` threw a swallowed "not a function" and silently never
  did its work. The request itself still resolves synchronously; only the callbacks moved.
- `Element.prototype.scrollTo`/`scrollBy`/`scroll`/`scrollIntoView` exist as no-ops, mirroring the window-level
  shims. Only the window half was installed, which is the half a page rarely calls; a component that scrolls an
  element while initializing (a carousel, a sticky nav, a cookie banner) threw on the missing method, and the
  throw landed inside that init and cost every link below it.

## [3.3.2] - 2026-07-15

### Added

- Opt-in execution of runtime-injected cross-origin scripts (`JsRenderOptions.ExecuteCrossOriginScripts`,
  off by default): a `<script src>` appended while the page runs that points at another host is executed
  rather than left pending. Off remains right for crawling — a tag manager's vendor SDK contributes no
  links and costs a cross-origin fetch plus a slow evaluation each — but a render whose purpose is to
  observe what a page *installs* needs them: the container runs from the page's own origin and injects the
  SDK from the vendor's, so skipping it reports a site running a tag manager as running none. Measured
  against a real browser, leaving them pending accounted for roughly half of the JavaScript globals a page
  defines (`google_tag_manager`, `gtag`, `Optanon` and similar). Scripts in the initial HTML were already
  executed regardless of origin; this governs only runtime-appended nodes.

## [3.3.1] - 2026-07-15

### Added

- Public collector-slice render surface (`JsRenderer.CollectAsync`): renders a shell and returns only the
  per-collector JSON slices the registered `IRenderedDomCollector` fragments produced, keyed by collector.
  This is `ExtractAsync`'s surface for a consumer that renders without crawling — same engine, same drain,
  same per-fragment isolation, but no anchors/canonical/meta-robots and no anchor walk to pay for. The
  crawl-shaped `ExtractAsync` stays internal and unchanged. Returns empty when no collectors are registered.
- Engine-only DI registrations (`AddV8JsEngine`, `AddJintJsEngine`): register just an unkeyed
  `IJsEngineFactory` (plus `V8EngineOptions` for V8), with no crawler, robots client, or `CrawlerOptions`.
  Previously the engine factories were internal and registered only by `AddV8Crawler`/`AddJintCrawler` under
  an internal-const key, so a consumer that drives `JsRenderer` itself had to stand up an entire crawl
  pipeline and then resolve the factory by a key that was never public. The crawler registrations are
  unchanged, and the two can coexist in one container.

## [3.3.0] - 2026-07-13

### Added

- Max crawl depth (`CrawlerOptions.MaxDepth`, CLI `--maxDepth`; `0` = unlimited): entry points are depth 0 and
  each followed link one deeper, so a link beyond the limit is not crawled. The depth a page was reached at is
  surfaced per URL on `UrlReport.Depth`. A URL over the limit is left undiscovered, so a later shorter path can
  still reach it.
- URL normalization before de-duplication (`CrawlerOptions.NormalizeUrls`, CLI `--normalizeUrls`, on by
  default): drops the `#fragment`, lowercases scheme/host, removes the default port, and collapses a trailing
  slash; the query string is left as-is (no reordering). A page's `<link rel="canonical">` still determines the
  emitted URL, so distinct request URLs that share a canonical collapse to one result line.
- Include/exclude link filters (CLI `--include`/`--exclude`, repeatable;
  `CrawlerOptions.IncludePatterns`/`ExcludePatterns`): robots.txt-style path globs (`*` wildcard, `$`
  end-anchor) applied to discovered links only — entry points are always crawled, on every backend. New
  `SimpleCrawler.Core.Filtering.UrlFilter` reuses the robots matcher's longest-match/allow-wins resolution;
  excludes deny, includes default-deny everything unmatched, and an exclude out-matches an include of equal
  length.
- Opt-in per-page signal capture (`CrawlerOptions.CapturePageSignals`, CLI `--captureSignals`, off by default):
  records each fetched page's response headers, cookie names, `<script src>` sources, meta tags, and
  `application/ld+json` blocks onto a new `UrlReport.Signals`, surfaced in the `--report` output. It runs on
  every backend — static, JS, and headless. Header keys are lower-cased and a header that appears more than
  once is joined with a newline (never comma-joined, since `Set-Cookie` values embed commas in their expiry
  dates); cookie values are dropped. Off by default because `UrlReport` is checkpointed and the whole
  checkpoint is rewritten on every autosave, so capturing every page would bloat each autosave with the full
  crawl history rather than just the in-flight URLs.
- Pluggable per-page collectors (new `SimpleCrawler.Core.Collectors` namespace): an `ICrawlCollector` observes
  every page's HTTP response, and an `IDomCollector` additionally derives data from the DOM. Because there is
  no shared C# DOM across backends, the DOM half is expressed once as an `IPageDom` walk for the static
  backends (`IStaticDomCollector`) and once as an in-page JavaScript fragment for the rendered backends
  (`IRenderedDomCollector.DomScript`) — no backend carries any collector-specific code. Register collectors
  via `AddCrawlCollectors` (idempotent per implementation, so several backends in one container never
  double-register); they run for every backend with no change to the core pipeline or the backends, and each
  fragment is isolated so a throw or an unserializable result yields no data for that collector without
  disturbing the crawl or the others. The built-in `PageSignalsCollector` behind `--captureSignals` is one
  such collector.

### Changed

- Updated `Jint` dependency to `4.12.0`
- Checkpoint format: the full discovered-URL set is no longer serialized. `CrawlState` now persists only the
  pending frontier with per-URL depth (`Frontier`) and rebuilds the in-memory `Discovered` on load from
  `Processed` plus the frontier. A completed crawl checkpoints an empty frontier, so the file shrinks rather
  than growing with depth. Checkpoints written by earlier versions are not resumed (the crawl restarts).

### Security

- Narrowed the V8 rendering engine's document access from `EnableAllLoading` to `EnableWebLoading`. All
  module imports are served by the custom `V8ModuleLoader`, whose fetcher only accepts http/https, so file
  loading was never needed; withholding `EnableFileLoading` removes a latent local-file-read path that
  untrusted page JS could otherwise resolve an `import()` toward.

## [3.2.0] - 2026-07-11

### Added

- Opt-in WebGL stub for the JS rendering backends (`JsRenderOptions.EnableWebGl`, CLI `--webgl`): map/3D
  libraries (Mapbox GL, Three.js, deck.gl) initialize WebGL synchronously while constructing and throw
  "Failed to initialize WebGL." on a null context, an uncaught throw that trips the SPA error boundary and
  drops every link on the page. When enabled, `canvas.getContext("webgl"/"webgl2")` returns a non-faulting
  stub context that reports success through setup so the surrounding page renders. Off by default (the map
  yields no anchors, and once initialized such a library may start fetching tiles).

## [3.1.0] - 2026-07-11

### Added

- Live crawl-time ETA (on by default): a periodic log line projecting remaining time from the crawl's own
  throughput/discovery history, withheld until the frontier drains steadily. New `SimpleCrawler.Core.Progress`
  namespace; CLI `--progress`, `--progressInterval`, `--progressConfirm`.

### Changed

- Replaced the shared FIFO crawl frontier with a host-partitioned one
  (`SimpleCrawler.Core.Scheduling.HostFrontier`) to end cross-site head-of-line blocking: a single
  dispatcher spaces each host and hands workers only ready URLs, so a slow host no longer stalls other
  sites. Equally-ready hosts round-robin.
- Updated `Simple.Logging.Console` to 2.0.0 and switched the CLI to the new truecolor `AddRgbConsoleLogging()`
  (`AddConsoleLogging()` still exists for ANSI); a warm `Write` is now allocation-free.
- Normalized per-page log lines across backends (`Response '{code}' from '{url}'` / `Error …`, numeric code
  everywhere) and proxy references (`via '{proxy}'`, `'direct connection'` when none); `ProxyInfo.ToString()`
  emits a clean `scheme://host:port`.
- Checkpointing now logs its activity (start line with target+interval, debug line per write, failures name
  the target); `ICheckpointStore` gained a `Target` property.

### Fixed

- JS DOM: closed three shim gaps that tripped SPA error boundaries mid-render. `IntersectionObserverEntry` is
  now a global carrying the standard entry fields on its prototype (the `'isIntersecting' in
  IntersectionObserverEntry.prototype` support probe threw a `ReferenceError`); `<canvas>` is a real
  `HTMLCanvasElement` with reflected `width`/`height` and a no-op 2D context from `getContext('2d')`
  (animation libraries grabbing a context synchronously threw); and `getComputedStyle` returns a
  `CSSStyleDeclaration` whose properties read back `""` by name or as direct properties (a direct `.content`
  read was `undefined`, so Elementor's `getCurrentDeviceMode` did `undefined.replace(...)`).

## [3.0.0] - 2026-07-09

### Added

- Custom request headers via a repeatable `-H`/`--header "Name: Value"`, merged into the browser profile so
  every backend gets them; `--cookie` flows through the same path.
- Adaptive per-host throttling (on by default): raises a host's delay after repeated `429`/`503` (honouring
  `Retry-After`) and eases it back on success. `ThrottleOptions` on `CrawlerOptions.Throttling`; CLI
  `--adaptiveThrottle`.
- Checkpoint/resume via `--checkpoint <file>`: the frontier is saved periodically and on `Ctrl+C`, and
  resumed when entry points match. New `SimpleCrawler.Core.Checkpoints` and `SimpleCrawler.Core.Throttling`
  namespaces; `AbstractCrawler` gained an optional `ICheckpointStore` ctor param (cadence via
  `CrawlerOptions.Checkpoint.Interval`).
- Per-URL reporting on `IScrapeResult.Reports` (`UrlReport`/`CrawlOutcome`): status, fetch/parse durations,
  size/type, link count, index/follow, timestamp, outcome, error. `Urls` unchanged. Optional `--report <file>`
  writes it as JSON.
- Opt-in WHATWG Streams for the JS backends via `JsRenderOptions.EnableStreams` (off by default):
  `ReadableStream`/`TransformStream`/`TextDecoderStream`/`Response.body` from a separate `stream.js` prelude;
  bodies are buffered-complete, not incremental. The renderer captures a pre-script anchor baseline and
  restores it if a streaming bundle (e.g. RSC) tears down the server markup.
- JS renderer exception diagnostics via an unconditional `__crawlerDiagnostic` channel at `Debug` (message
  before stack).
- Cross-page render fetch cache for the JS backends (`EnableFetch`), honouring standard
  `Cache-Control`/`Pragma`/`Vary` opt-outs and storing only 2xx responses.
- Next.js RSC prefetches (`Next-Router-Prefetch`) are short-circuited with a `204`.

### Changed

- Per-host throttling extracted into `AdaptiveThrottler`, reworked from a held semaphore to a lock-free
  next-slot reservation so rate-limit reporting never blocks. Internal, source-compatible; spacing unchanged.
- `IJsEngine` no longer requires `IDisposable`; engines own their teardown and `JsRenderer` disposes them on
  every exit path (the V8 pool no longer leaks isolates). **Breaking** for custom `IJsEngine` implementers.
- Updated `PuppeteerSharp` to >= 25.0.4.

### Removed

- CLI `--proxyRetries` removed (deprecation period over).
- **Breaking:** removed the dead bridge-era `IJsEngine` members `GetGlobalObject`, `CreateArray`,
  `InvokeCallback`, `EmbedHostType`, and the unreachable `__crawlerReset` prelude machinery.
- Removed the Jint `Map.keys()`/`values()` iterator compat shim — fixed upstream in Jint 4.11.

### Fixed

- robots-meta parsing defaulted `index`/`follow` to `false`, so a lone `content="index"` dropped every link;
  the flags now default `true` (directives only negate). Pinned by `IndexingHelperTests`.
- Closed RSC-adjacent DOM shim gaps that crashed Next.js App Router hydration (live `Element.attributes`
  `NamedNodeMap`, `removeAttributeNode`/`getAttributeNode`/`setAttributeNode`, `Node.getRootNode`,
  `getElementsByName`, `reportError`, `document.readyState` + `DOMContentLoaded`). RSC renders fully on V8;
  Jint still fails on React Flight deserialization (upstream bug).
- Checkpoint/resume was unreachable for the AngleSharp/JS/Playwright/Puppeteer backends (ctors didn't forward
  `ICheckpointStore`); all now thread it through.
- `Ctrl+C` raced the checkpoint save; the CLI now cancels on the first `Ctrl+C`, waits for in-flight work and
  the checkpoint, and exits immediately on a second.
- JS DOM: added a spec-shaped no-op Constraint Validation API + `FileList` global (frameworks calling
  `setCustomValidity`/`checkValidity` or touching `.files` threw), plus a `CSSTransition` shim and
  null-returning `getComputedStyle`.
- Jint `Function.prototype.toString()` returned `"[native code]"` for all functions, breaking jQuery/Sizzle's
  native-code sniff; prepared scripts are now tagged with their source so real text is returned.

## [2.0.0] - 2026-07-07

Public proxy types are removed and renamed (see _Removed_ and _Changed_), a breaking change under SemVer.

### Added

- Generalized retry for every backend (with or without a proxy pool): connection errors, timeouts, `429`, and
  `5xx` are retried with exponential backoff and jitter.
- `RetryOptions` on `CrawlerOptions.Retry` (`MaxRetries`, `BaseDelay`, `MaxDelay`, `JitterFactor`,
  `DelayOnRateLimit`, `AttemptTimeout`); headless backends default to `MaxRetries = 1`. A per-attempt timeout
  cancels and retries a stalled request. CLI `--retries`, `--retryDelay`, `--maxRetryDelay`, `--attemptTimeout`.
- New `SimpleCrawler.Core.Retry` namespace (`RetryExecutor`, `RetryClassifier`, `RetryReason`,
  `RetryAttempt<T>`, `RetryHandler`).
- `AbstractHeadlessCrawler<TPage, TResult>`: a shared base owning the proxy-keyed page pool, acquire/retry
  loop, and single-evaluation extraction; backends supply `NewPageAsync`/`NavigateAsync`/`ClosePageCore`/
  `EvaluateExtractorAsync` plus an optional `AfterSuccessfulLoad`.

### Changed

- Proxy rotation is now the "alternative route" case of the shared retry loop: multi-proxy rotates instantly,
  single/absent backs off, and `429` backs off even with a free proxy (`DelayOnRateLimit`, default on).
- **Breaking:** `IProxyPool.ReportFailure` takes `RetryReason` instead of `ProxyFailureKind`.
- Retry is installed at the HTTP layer for `HttpClient` backends, so robots/sitemap probes and JS sub-resource
  fetches inherit it; the request timeout is uncapped and `AttemptTimeout` bounds each attempt.
- `PlaywrightCrawler<TResult>`/`PuppeteerCrawler<TResult>` now derive from `AbstractHeadlessCrawler<IPage,
  TResult>` (removes ~90% of the duplication); source-compatible, subclasses still override only `GetResult`.

### Removed

- **Breaking:** removed `ProxyFailureKind`, `ProxyAttempt<T>`, `ProxyFailureClassifier`, `ProxyRetryExecutor`,
  and `ProxyRoutingHandler` — superseded by the `Retry` namespace.
- **Breaking:** removed `ProxyPoolOptions.MaxRetries`; the retry budget moved to `RetryOptions.MaxRetries`.

### Compatibility

- The CLI stays backward-compatible: `--proxyRetries` is a hidden alias for `--retries`.

## [1.0.0] - 2026-07-07

- Initial release.

[Unreleased]: https://github.com/svenrog/SimpleCrawler/compare/v3.5.1...HEAD
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
