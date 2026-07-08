# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Package versions are derived from git tags (`v*`) via MinVer.

## [Unreleased]

### Added

- Custom request headers via a repeatable `-H`/`--header "Name: Value"` CLI flag. Headers are merged
  into the active browser profile, so they apply to every backend (static, JS, and headless). `--cookie`
  now flows through the same path and reaches headless backends too.
- Adaptive per-host throttling (on by default): after repeated `429`/`503` responses a host's crawl
  delay is raised — honouring the `Retry-After` header as a per-host grace — and eased back down on
  sustained success. Configurable via `ThrottleOptions` on `CrawlerOptions.Throttling`
  (`Enabled`, `MaxDelaySeconds`) and the CLI flag `--adaptiveThrottle` (pass `false` to disable).
- Checkpoint/resume via `--checkpoint <file>`: the crawl frontier (discovered/processed/visited) is
  saved periodically and on `Ctrl+C`, and resumed automatically when the file's entry points match.
- New `SimpleCrawler.Core.Checkpoints` namespace (`ICheckpointStore`, `CrawlState`, `CheckpointOptions`)
  and `SimpleCrawler.Core.Throttling` namespace (`AdaptiveThrottler`, `ThrottleOptions`).
  `AbstractCrawler` gained an optional `ICheckpointStore` constructor parameter; autosave cadence is
  configured via `CrawlerOptions.Checkpoint.Interval`.
- Per-URL reporting on `IScrapeResult` via a new `Reports` collection of `UrlReport` (in
  `SimpleCrawler.Core.Models`, alongside the `CrawlOutcome` enum). Every fetched page — success or
  failure — is reported with its status code, fetch/parse durations, content length/type, discovered
  link count, index/follow flags, timestamp, outcome, and any error. `Urls` is unchanged (still the
  indexable subset). Reports live in `CrawlState`, so they are checkpointed and restored on resume in
  step with `Urls`.
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

### Changed

- Per-host throttling was extracted into an `AdaptiveThrottler` service and reworked from a semaphore
  held across the delay to a lock-free next-slot reservation, so rate-limit reporting never blocks on
  an in-flight wait. Internal and source-compatible; per-host request spacing is unchanged.
- `IJsEngine` no longer requires `IDisposable`. The concrete engines own their teardown — `V8JsEngine`
  returns its pooled isolate lease and disposes the native engine, `JintJsEngine` disposes the Jint
  engine — and `JsRenderer` disposes the concrete engine on every exit path so the V8 pool never leaks
  isolates. **Breaking** for custom `IJsEngine` implementers: the interface no longer extends
  `IDisposable`.

### Removed

- Retries in `smpcrawl` CLI `--proxyRetries` is removed, deprecation period over.
- **Breaking:** removed the dead bridge-era members `GetGlobalObject`, `CreateArray`,
  `InvokeCallback`, and `EmbedHostType` from the public `IJsEngine` interface — relics of the
  C#↔JS DOM bridge (cut in Phase 6), never invoked by any renderer path or test. The unreachable
  `__crawlerReset` realm-reset machinery was also deleted from the JS DOM prelude; it existed only for
  the since-removed Jint realm pool and is internal cleanup.

### Fixed

- robots-meta parsing started `index`/`follow` at `false`, so a lone `content="index"` (no explicit
  `follow`) parsed as `follow=false` and the crawler dropped every link on the page. The spec defaults
  are `index, follow` and directives only negate, so the flags now start `true`; lone and combined
  directives keep their permissive defaults. Pinned by `IndexingHelperTests`.

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

[Unreleased]: https://github.com/svenrog/SimpleCrawler/compare/v2.0.0...HEAD
[2.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v2.0.0
[1.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v1.0.0
