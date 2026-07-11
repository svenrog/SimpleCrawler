# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Package versions are derived from git tags (`v*`) via MinVer.

## [Unreleased]

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

### Changed

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

[Unreleased]: https://github.com/svenrog/SimpleCrawler/compare/v3.2.0...HEAD
[3.2.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.2.0
[3.1.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.1.0
[3.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.0.0
[2.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v2.0.0
[1.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v1.0.0
