# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Package versions are derived from git tags (`v*`) via MinVer.

## [Unreleased]

### Added

- Live crawl-time estimation (on by default): a periodic log line projecting how long the crawl still
  has to run, inferred from the crawl's own recent history. A background reporter samples the
  processed/discovered counts into a bounded window; `CrawlProgressEstimator` fits throughput
  (`mu = dP/dt`) and discovery yield (`g = dD/dP`) and projects the remaining work as the geometric
  frontier drain `F / (1 - g)`, with an optimistic/pessimistic ETA band from the regression's standard
  error. Because a discovery cliff (a page revealing a whole new section) can't be predicted from local
  history, an ETA is withheld until the frontier has been contracting for a sustained period, and a
  burst that pushes yield back over `1` reverts to "expanding" — surfacing as a
  WarmingUp → Expanding → Draining → Estimating progression rather than a confident-but-wrong number
  that whipsaws. New `SimpleCrawler.Core.Progress` namespace (`CrawlProgressEstimator`, `ProgressOptions`,
  `ProgressEstimate`, `ProgressState`) configured via `CrawlerOptions.Progress`, and the CLI flags
  `--progress` (default on), `--progressInterval <seconds>`, and `--progressConfirm <seconds>` (how long
  the queue must keep shrinking before an ETA is shown).

### Changed

- Updated `Simple.Logging.Console` dependency to 2.0.0 and switched the CLI to the new 24-bit
  truecolor formatter via `AddRgbConsoleLogging()` (was `AddConsoleLogging()`, which still exists for
  ANSI output). The 2.0.0 rewrite makes a warm `Write` allocation-free.
- Normalized crawl log messages across all backends: the per-page success/error lines now read
  identically (`Response '{code}' from '{url}'` / `Error '{code}' from '{url}'`) with the numeric
  status code everywhere (static/JS previously logged the `HttpStatusCode` name), and proxy references
  use a single `via '{proxy}'` form that renders `'direct connection'` when no proxy is configured
  instead of a dangling `via proxy `. The proxy is no longer appended to the headless success line,
  since the HttpClient path can't surface it there. `ProxyInfo.ToString()` now emits a clean
  `scheme://host:port` (no trailing slash).
- Checkpointing now logs its activity: an informational line on start naming the target file and
  interval, and a debug line on each write (autosave, final save, and `Ctrl+C`). Read/write failures
  name the target. `ICheckpointStore` gained a `Target` property describing where checkpoints are
  persisted.

## [3.0.0] - 2026-07-09

### Added

- Custom request headers via a repeatable `-H`/`--header "Name: Value"` CLI flag. Headers are merged
  into the active browser profile, so they apply to every backend (static, JS, and headless). `--cookie`
  now flows through the same path and reaches headless backends too.
- Adaptive per-host throttling (on by default): after repeated `429`/`503` responses a host's crawl
  delay is raised, honouring the `Retry-After` header as a per-host grace, and eased back down on
  sustained success. Configurable via `ThrottleOptions` on `CrawlerOptions.Throttling`
  (`Enabled`, `MaxDelaySeconds`) and the CLI flag `--adaptiveThrottle` (pass `false` to disable).
- Checkpoint/resume via `--checkpoint <file>`: the crawl frontier (discovered/processed/visited) is
  saved periodically and on `Ctrl+C`, and resumed automatically when the file's entry points match.
- New `SimpleCrawler.Core.Checkpoints` namespace (`ICheckpointStore`, `CrawlState`, `CheckpointOptions`)
  and `SimpleCrawler.Core.Throttling` namespace (`AdaptiveThrottler`, `ThrottleOptions`).
  `AbstractCrawler` gained an optional `ICheckpointStore` constructor parameter; autosave cadence is
  configured via `CrawlerOptions.Checkpoint.Interval`.
- Per-URL reporting on `IScrapeResult` via a new `Reports` collection of `UrlReport` (in
  `SimpleCrawler.Core.Models`, alongside the `CrawlOutcome` enum). Every fetched page is reported with
  its status code, fetch/parse durations, content length/type, discovered link count, index/follow flags, 
  timestamp, outcome, and any error. `Urls` is unchanged (still the indexable subset).
- Optional `--report <file>` CLI flag that writes the per-URL report as JSON. The existing plain
  URL-per-line output is unchanged.
- Opt-in WHATWG Streams surface for the JS backends via `JsRenderOptions.EnableStreams` (off by
  default). It installs `ReadableStream`, `TransformStream`, `TextDecoderStream`, and a
  `Response.body`/`arrayBuffer` from a dedicated `stream.js` prelude kept out of `dom.js`, so the
  default render neither evaluates it nor exposes the stream globals. Bodies are buffered-complete —
  spec-compliant reader/transform semantics, not incremental transport streaming. A streaming hydration
  bundle (e.g. Next.js App Router RSC) can otherwise tear down the server markup without rebuilding it
  in this single-pass render, so the renderer captures a pre-script anchor baseline and restores it at
  finalize when the live tree regresses below the shell's links.
- Surfaced exception diagnostics for the JS renderer. Catches now route through an unconditional
  `__crawlerDiagnostic` channel at `Debug` level, so raising the log level to `Debug` turns
  every silent settle into a named exception with a stack. The message is emitted before
  the stack: Jint's `error.stack` is frames-only, so reporting the stack alone dropped 
  what identifies the failure.
- Cross-page render fetch cache for the JS backends (`EnableFetch`). Cache hits log at
  `Debug` as `Render fetch (cache hit)`. The cache honours the standard opt-outs — request
  `Cache-Control: no-store`/`no-cache` and `Pragma: no-cache`, response `Cache-Control`
  `no-store`/`no-cache`/`private`/`max-age=0` and `Vary: *`. It stores only successful (2xx) responses,
  so a transient `429`/`5xx` responses are never replayed.
- Next.js App Router RSC prefetches (requests carrying `Next-Router-Prefetch`) are now short-circuited with
  an empty `204`.

### Changed

- Per-host throttling was extracted into an `AdaptiveThrottler` service and reworked from a semaphore
  held across the delay to a lock-free next-slot reservation, so rate-limit reporting never blocks on
  an in-flight wait. Internal and source-compatible; per-host request spacing is unchanged.
- `IJsEngine` no longer requires `IDisposable`. The concrete engines own their teardown — `V8JsEngine`
  returns its pooled isolate lease and disposes the native engine, `JintJsEngine` disposes the Jint
  engine — and `JsRenderer` disposes the concrete engine on every exit path so the V8 pool never leaks
  isolates. **Breaking** for custom `IJsEngine` implementers: the interface no longer extends
  `IDisposable`.
- Updated `PuppeteerSharp` dependency to >= 25.0.4.

### Removed

- Retries in `smpcrawl` CLI `--proxyRetries` is removed, deprecation period over.
- **Breaking:** removed the dead bridge-era members `GetGlobalObject`, `CreateArray`,
  `InvokeCallback`, and `EmbedHostType` from the public `IJsEngine` interface — relics of the
  C#↔JS DOM bridge (cut in Phase 6), never invoked by any renderer path or test. The unreachable
  `__crawlerReset` realm-reset machinery was also deleted from the JS DOM prelude; it existed only for
  the since-removed Jint realm pool and is internal cleanup.
- The Jint `Map.keys()`/`values()` iterator compat shim and its shims prelude. Jint 4.11 fixed the
  "Collection was modified" bug it patched (a bundle mutating a `Map` during iteration), so the
  per-engine shim is no longer needed.

### Fixed

- robots-meta parsing started `index`/`follow` at `false`, so a lone `content="index"` (no explicit
  `follow`) parsed as `follow=false` and the crawler dropped every link on the page. The spec defaults
  are `index, follow` and directives only negate, so the flags now start `true`; lone and combined
  directives keep their permissive defaults. Pinned by `IndexingHelperTests`.
- RSC-adjacent DOM gaps that crashed Next.js App Router (RSC) sites during hydration/commit. The shim
  gaps the crashes traced to are closed: `Element.attributes` is now a live `NamedNodeMap`-backed
  collection that shrinks as attributes are removed (React's singleton teardown loop relied on the
  collection actually contracting), with real `removeAttributeNode`/`getAttributeNode`/`setAttributeNode`,
  plus `Node.getRootNode`, `document.getElementsByName`, a global `reportError`, and `document.readyState`
  with a `DOMContentLoaded` dispatch after bundle execution. App Router RSC sites now render fully on the
  default V8 engine; on Jint they still fail with "Cannot convert undefined or null to object" from React's
  Flight deserialization — an upstream engine bug tracked separately.
- Checkpoint/resume was only reachable from `AbstractCrawler`-based (static) crawlers: the concrete
  AngleSharp, JS (Jint/V8), Playwright, and Puppeteer constructors didn't forward an `ICheckpointStore`
  parameter, so DI could never supply one for those backends. All backend constructors now accept and
  thread through an optional `ICheckpointStore`.
- `Ctrl+C` cancelled the crawl token but let .NET terminate the process immediately afterward, racing
  the in-flight checkpoint save. The CLI now cancels on the first `Ctrl+C`, waits for in-flight requests
  to finish and the checkpoint to persist, and logs that it's doing so; a second `Ctrl+C` exits immediately.
- JS DOM: `HTMLElement` had no Constraint Validation API and there was no global `FileList`, so frameworks
  that call `setCustomValidity`/`checkValidity`/`reportValidity` on a form-control ref, or touch a file
  input's `.files`, threw during hydration. Added a no-op-but-spec-shaped Constraint Validation API
  (`willValidate`, `validity`, `checkValidity`, `reportValidity`, `setCustomValidity`) and a `FileList` global.
  Also added shim for `CSSTransition` and adjusted `getComputedStyle` to return null values.
- Jint's `Function.prototype.toString()` defaulted to a hardcoded `"[native code]"` stub for every
  ordinary script function (unlike V8/real browsers, which print real source), making bundle-authored
  functions indistinguishable from the host DOM methods `browser/native.ts` deliberately marks as native
  for jQuery/Sizzle's native-code sniff. Prepared scripts/modules are now tagged with their source text
  at parse time so `Options.Host.FunctionToStringHandler` can return the real text for everything else.

## [2.0.0] - 2026-07-07

Public proxy types are removed and renamed (see _Removed_ and _Changed_), which is a breaking
change to the library API under SemVer.

### Added

- Generalized retry for every backend (static, JS, headless), applied whether or not a proxy
  pool is configured. Transient failures — connection errors, timeouts, `429`, and `5xx` — are
  retried with exponential backoff and jitter.
- `RetryOptions` on `CrawlerOptions.Retry` (`MaxRetries`, `BaseDelay`, `MaxDelay`, `JitterFactor`,
  `DelayOnRateLimit`, `AttemptTimeout`). Headless backends default to `MaxRetries = 1`.
- Per-attempt timeout (`RetryOptions.AttemptTimeout`) that cancels and retries a stalled request.
- CLI flags `--retries`, `--retryDelay`, `--maxRetryDelay`, and `--attemptTimeout`.
- New `SimpleCrawler.Core.Retry` namespace: `RetryExecutor`, `RetryClassifier`, `RetryReason`,
  `RetryAttempt<T>`, `RetryHandler`.
- `AbstractHeadlessCrawler<TPage, TResult>` in `SimpleCrawler.Core`: a shared base for real-browser
  backends that owns the proxy-keyed page pool, the acquire/retry loop, and single-evaluation
  extraction. Backends supply four vendor primitives (`NewPageAsync`, `NavigateAsync`,
  `ClosePageCore`, `EvaluateExtractorAsync`) plus an optional `AfterSuccessfulLoad` hook.

### Changed

- Proxy rotation is now the "alternative route" case of the shared retry loop: a multi-proxy pool
  rotates instantly (no delay); a single-proxy or absent pool backs off; `429` backs off even when
  another proxy is free (`DelayOnRateLimit`, default on).
- **Breaking:** `IProxyPool.ReportFailure` now takes `RetryReason` instead of `ProxyFailureKind`.
- Retry is installed at the HTTP layer for `HttpClient`-based backends, so `robots.txt`/sitemap
  probes and JS sub-resource fetches inherit it. The crawler `HttpClient` request timeout is left
  uncapped; `RetryOptions.AttemptTimeout` bounds each attempt instead.
- `PlaywrightCrawler<TResult>` and `PuppeteerCrawler<TResult>` now derive from
  `AbstractHeadlessCrawler<IPage, TResult>` instead of `AbstractRobotsCrawler<IPage, IPage, TResult>`,
  removing ~90% of the duplication between them. This is source-compatible: the new base still
  derives from `AbstractRobotsCrawler<IPage, IPage, TResult>`, no public member was removed, and
  subclasses still need only override `GetResult`. Pre-compiled third-party subclasses should be
  recompiled against the new base.

### Removed

- **Breaking:** removed `ProxyFailureKind`, `ProxyAttempt<T>`, `ProxyFailureClassifier`,
  `ProxyRetryExecutor`, and `ProxyRoutingHandler` — superseded by the `Retry` namespace.
- **Breaking:** removed `ProxyPoolOptions.MaxRetries`; the retry budget moved to
  `RetryOptions.MaxRetries`.

### Compatibility

- The `smpcrawl` CLI stays backward-compatible: `--proxyRetries` is retained as a hidden alias for
  `--retries`.

## [1.0.0] - 2026-07-07

- Initial release.

[Unreleased]: https://github.com/svenrog/SimpleCrawler/compare/v3.0.0...HEAD
[3.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v3.0.0
[2.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v2.0.0
[1.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v1.0.0
