# CrawlerOptions

Source found in `src/SimpleCrawler.Core/CrawlerOptions.cs`, controls crawler behaviour.

### Concurrency (default 8)

Fetch workers to run simultaneously. I/O-bound, so can exceed core count; bounded by the target's tolerance and your network, not CPU. Pair high values with [`CrawlDelay`](#crawldelay) when crawling sites you don't own.

### ParseConcurrency (default 0 = match Concurrency)

Parse/render workers to run simultaneously. Parsing, especially when rendering JavaScript, is CPU-bound (having a higher count on this than CPU cores will hurt throughput).
For big page results, use a lower `ParseConcurrency` than `Concurrency`.

### MaxPages (default 10000)

This is a soft cap on pages processed (since threads are in flight, there might be a number of fetch threads additionally on this count). It can limit the crawler from continuing infinitely if a site has quirky query parameter handling that doesn't use canonical urls.

### MaxResponseBodySize (default 10 MiB)

Cap on the decompressed response body, in bytes. A response that exceeds it is skipped (logged, not parsed) rather than buffered into memory — a guard against a single oversized asset blowing up a many-worker crawl. `0` or less disables the cap. **Static backends only**; headless backends stream through the browser.

### CrawlDelay (default 1)

Minimum seconds between fetches to a host (per-host, not per-worker). `0` = no delay. If `RespectRobotsTxt` is on, a `robots.txt` `Crawl-delay` overrides upward. [Adaptive throttling](#throttling) can raise the effective delay further after rate-limit responses.

### RespectRobotsTxt (default true)

Don't just honor `robots.txt`, respect the rules and crawl-delay. Disable it if you dare, I might tell your mother.

### RespectMetaRobots (default true)

Honor `<meta name="robots">`. `noindex` pages are fetched for their links but excluded from results; `nofollow` pages have their links ignored.

### EnableSitemapDiscovery (default true)

Seed from `sitemap.xml` (via `robots.txt`) in addition to following links. Disable to crawl only what's reachable from the entry URL (this is used for testing mainly).

### BlockNonEssentialResources (default true)

**Headless crawlers only.** Aborts images/CSS/fonts/media so the browser loads only what's needed for links. Large bandwidth/time saving, no effect on extraction. Ignored by static/JS crawlers.

> `BlockNonEssentialResources` and `NetworkIdleGraceMs` live on `HeadlessCrawlerOptions` (`src/SimpleCrawler.Core/HeadlessCrawlerOptions.cs`), a subclass of `CrawlerOptions`. Construct it from an existing `CrawlerOptions` with `new HeadlessCrawlerOptions(options) { … }`, or on its own. All the options above apply to it too.

### NetworkIdleGraceMs (default 2000)

**Headless crawlers only.** Upper bound, in milliseconds, on the best-effort wait for network idle after a page loads. A page that never goes idle — constant analytics/tracking traffic — is extracted once this elapses rather than failing, so it caps per-page latency rather than guaranteeing idle.

## Retry

Every fetch is retried on transient failures, connection errors, timeouts, `429`, and `5xx` — across **all** backends, with or without a proxy pool. Backoff is exponential with jitter, and a per-attempt timeout cancels and retries a stalled request. Non-transient responses (`404`, `403`, …) are returned as-is, never retried.

`RetryOptions` (`src/SimpleCrawler.Core/Retry/RetryOptions.cs`):

| Option | Default | |
| --- | --- | --- |
| `MaxRetries` | `3` (headless `1`) | Extra attempts after the first. |
| `BaseDelay` | `500ms` | First backoff; doubles each retry. |
| `MaxDelay` | `30s` | Backoff ceiling. |
| `JitterFactor` | `0.2` | ±20% randomisation on each delay. |
| `DelayOnRateLimit` | `true` | Back off on `429` even when another proxy is free. |
| `AttemptTimeout` | `100s` | Per-attempt cap; `0` disables. |

```csharp
using SimpleCrawler.Core.Retry;

var options = new CrawlerOptions
{
    Retry = new RetryOptions { MaxRetries = 5, BaseDelay = TimeSpan.FromSeconds(1) },
};
```

With a multi-proxy pool a retry rotates to the next proxy instantly (no delay); without one — or on a single-proxy pool — it backs off. See [proxy pooling](./httpclient-configuration.md#headless-backends). The CLI exposes `--retries`, `--retryDelay`, `--maxRetryDelay`, and `--attemptTimeout`.

## Throttling

Adaptive per-host throttling (on by default). After repeated `429`/`503` responses a host's effective delay is raised — honouring the response's `Retry-After` header as a per-host grace period — and eased back down again on sustained success. It layers on top of the fixed [`CrawlDelay`](#crawldelay) floor and reacts to a host that is actively pushing back, so you don't have to hand-tune a single delay for the whole crawl.

`ThrottleOptions` (`src/SimpleCrawler.Core/Throttling/ThrottleOptions.cs`):

| Option | Default | |
| --- | --- | --- |
| `Enabled` | `true` | Set `false` for a fixed `CrawlDelay` only. |
| `MaxDelaySeconds` | `60` | Ceiling on the total effective delay (base + rate-limit penalty) for a host. |

The CLI toggles it with `--adaptiveThrottle` (pass `false` to disable).

## Checkpoint

When a checkpoint store is registered, the full crawl state is snapshotted periodically and on `Ctrl+C`, and a matching checkpoint resumes the crawl instead of starting over.

`CheckpointOptions` (`src/SimpleCrawler.Core/Checkpoints/CheckpointOptions.cs`):

| Option | Default | |
| --- | --- | --- |
| `Interval` | `15s` | Autosave cadence while crawling. |

The store itself is an `ICheckpointStore` passed to the crawler constructor; the CLI wires a JSON-file store via `--checkpoint <file>` and resumes automatically when the file's entry points match.

## ProxyPool

Optional health-aware, per-request proxy pool (`ProxyPoolOptions`). Off unless set. Configuration, rotation, SOCKS support, and authentication are covered under [HttpClient configuration](./httpclient-configuration.md); the CLI accepts a proxy or proxy-list reference via `--proxy` plus `--proxyCooldown` and `--proxyMinHealthy`.

## BrowserProfile

The identity presented to the target: `User-Agent`, `Accept`/`Accept-Language`, `Locale`, and
any extra request headers. The `User-Agent` is also the token matched against `robots.txt`.

- `DefaultBrowserProfile` (default) — an basic crawler profile that sets `SimpleCrawler/…` user agent.
- `ChromeBrowserProfile` — **browser impersonation**: a current Chrome `User-Agent` plus some additional client hints and scripts that imitate a browser with a head.

```csharp
var options = new CrawlerOptions
{
    BrowserProfile = new ChromeBrowserProfile(), 
    // or new DefaultBrowserProfile { UserAgent = "my-crawler/1.0" }
};
```

`BrowserProfiles.Default` / `BrowserProfiles.Chrome` expose shared instances; the CLI selects
between them with the `--impersonate` flag (`none` / `chrome`).

Extra headers layer on top of the chosen profile and apply to **every** backend (static, JS, headless):
the CLI adds them with the repeatable `-H`/`--header "Name: Value"` flag, and `--cookie` sets the
`Cookie` header through the same path. A supplied header overrides a matching profile default.
