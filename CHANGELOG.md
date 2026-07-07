# Changelog

All notable changes to this project are documented here.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).
Package versions are derived from git tags (`v*`) via MinVer.

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

### Changed

- Proxy rotation is now the "alternative route" case of the shared retry loop: a multi-proxy pool
  rotates instantly (no delay); a single-proxy or absent pool backs off; `429` backs off even when
  another proxy is free (`DelayOnRateLimit`, default on).
- **Breaking:** `IProxyPool.ReportFailure` now takes `RetryReason` instead of `ProxyFailureKind`.
- Retry is installed at the HTTP layer for `HttpClient`-based backends, so `robots.txt`/sitemap
  probes and JS sub-resource fetches inherit it. The crawler `HttpClient` request timeout is left
  uncapped; `RetryOptions.AttemptTimeout` bounds each attempt instead.

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

[2.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v2.0.0
[1.0.0]: https://github.com/svenrog/SimpleCrawler/releases/tag/v1.0.0
