# Configuration

This section describes configuration in detail, when running the CLI isn't enough.

- [CrawlerOptions](./crawler-options.md), options that control crawling behaviour.
- [JavaScript crawlers](./js-tier.md) — Jint vs V8, HTML parser, `JsRenderOptions`.
- [Performance](./performance.md) — measured numbers.

## Pipeline

`Crawler.Core` is a two-stage fetch→parse pipeline driven by one `CrawlerOptions`. A backend only implements
`LoadResponse()` (load a page) and `ExtractPageData()` (pull links); everything else — concurrency, robots,
sitemap, visited-set, link resolution — is shared. Swapping backend = one DI call.

<p align="center"><img src="./pipeline.svg" alt="Shared CrawlerOptions drives a fetch→parse pipeline that feeds a pluggable Static / JS / Headless backend; discovered links loop back to discovery." width="800"></p>

Fetch is I/O-bound, parse is CPU-bound — splitting them lets you run many fetches while capping parses near
core count. See [`ParseConcurrency`](./crawler-options.md#parseconcurrency).

## Tiers

Trade fidelity for cost. Start at the cheapest tier that returns the links you need.

| Tier         | Runs JS | Browser | Cost\* | Use when                                                |
| ------------ | ------- | ------- | ------ | ------------------------------------------------------- |
| **Static**   | no      | no      | 1×     | links are in the server HTML (SSR, MPA, classic CMS)    |
| **JS**       | yes     | no      | ~5–30× | client-only SPA builds links at runtime, standard APIs  |
| **Headless** | yes     | yes     | ~50–100× | needs a real browser (RSC streaming, canvas, workers) |

\* Per-page, relative to a static parse. Measured numbers: [performance](./performance.md).

**Static** (`HtmlAgilityPack`, `AngleSharp`) — one HTTP request + HTML parse, no scripts. HtmlAgilityPack:
default, fast, forgiving of bad markup, lowest allocations. AngleSharp: spec-compliant WHATWG, slightly
heavier. Misses anything injected by client JS.

**JS** (`Crawler.Js`) — fetches the shell, builds an in-process DOM (`dom.js`), runs scripts in Jint or V8,
extracts links. Renders real React/Preact/Solid/Svelte/Vue/jQuery without a browser. It's a DOM shim, not a
browser: RSC streaming, service workers, canvas/WebGL are out of scope — use headless for those. Two-part
choice (engine + parser): [JS tier](./js-tier.md).

**Headless** (`Playwright`, `Puppeteer`) — real Chromium, max fidelity, max cost. Fallback when the JS shim
can't render a site. Both honour
[`BlockNonEssentialResources`](./crawler-options.md#blocknonessentialresources). Needs a browser binary;
unfriendly to AOT.

## Wiring

Shared options object (see [CrawlerOptions](./crawler-options.md)):

```csharp
var options = new CrawlerOptions
{
    Concurrency = 16,
    ParseConcurrency = 8,
    MaxPages = 5000,
    UserAgent = "my-crawler/1.0",
};
```

### Static

```csharp
using Crawler.HtmlAgilityPack;       // or Crawler.AngleSharp

var services = new ServiceCollection();
services.AddHtmlAgilityPackCrawler(options, (provider, client) =>
    client.DefaultRequestHeaders.Add("Cookie", "session=..."));   // optional HttpClient hook

var crawler = services.BuildServiceProvider().GetRequiredService<DefaultHtmlAgilityPackCrawler>();
var result = await crawler.Start("https://example.com/");         // result.Urls = discovered set
```

`AddAngleSharpCrawler` → `DefaultAngleSharpCrawler`, same shape.

### JS (engine + parser)

Register one engine and at most one `IHtmlParser` (renderer takes the first; none = `dom.js`'s own tokenizer).

```csharp
using Crawler.Js.V8;                 // engine: or Crawler.Js.Jint
using Crawler.Js.HtmlAgilityPack;    // parser: or Crawler.Js.AngleSharp, or omit

var renderOptions = new JsRenderOptions { EnableFetch = true };

var services = new ServiceCollection();
services.AddV8Crawler(options, renderOptions);
services.AddHtmlAgilityPackHtmlParser();

var crawler = services.BuildServiceProvider().GetRequiredService<DefaultV8Crawler>();
var result = await crawler.Start("https://example.com/");
```

`AddV8Crawler`↔`AddJintCrawler`, `AddHtmlAgilityPackHtmlParser`↔`AddAngleSharpHtmlParser`. Resolve
`DefaultV8Crawler`/`DefaultJintCrawler`.

### Headless

```csharp
using Crawler.Playwright;            // or Crawler.Puppeteer

var services = new ServiceCollection();
services.AddPlaywrightCrawler(options);   // no HttpClient hook; configure via browser context

var crawler = services.BuildServiceProvider().GetRequiredService<DefaultPlaywrightCrawler>();
var result = await crawler.Start("https://example.com/");
```

`AddPuppeteerCrawler` → `DefaultPuppeteerCrawler`.
