# Configuration

This section describes configuration in detail.

- [CrawlerOptions](./crawler-options.md)
- [JavaScript crawlers](./javascript-crawlers.md)
- [HttpClient configuration](./httpclient-configuration.md)
- [Performance](./performance.md)

## Pipeline

`SimpleCrawler.Core` is a two-stage fetch → parse pipeline. A backend only implements
`LoadResponse()` (load a page) and `ExtractPageData()` (extract links); everything else is shared. Swapping backend is one DI call.

<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="./pipeline-dark.svg">
    <img src="./pipeline.svg" alt="Shared CrawlerOptions drives a fetch→parse pipeline that feeds a pluggable Static / JS / Headless backend; discovered links loop back t
o discovery." width="800">
  </picture>
</p>

## Tiers

Choosing a crawler implementation is a cost/fidelity tradeoff.

| Tier         | Runs JS | Is a browser | Cost\* | Use when                                                |
| ------------ | ------- | ------- | ------ | ------------------------------------------------------- |
| **Static**   | no      | no      | 1×     | links are in the server HTML (SSR, MPA, classic CMS)    |
| **JS**       | yes     | no      | ~5–30× | client-only SPA builds links at runtime, standard APIs  |
| **Headless** | yes     | yes     | ~50–100× | needs a real browser (RSC streaming, canvas, workers) |

\* Resource use (CPU time) per-page. Detailed numbers found under [performance](./performance.md).

**Static** (`HtmlAgilityPack`, `AngleSharp`) crawlers do one HTTP request, then a HTML parse, they run no scripts and miss anything injected by client JS.

**JS** (`SimpleCrawler.Js`) crawlers fetches the shell, builds an in-process DOM (`dom.js`), runs scripts in Jint or V8, extracts links. Renders real markup without a browser. Two-part
choice (engine + parser): [JavaScript crawlers](./javascript-crawlers.md).

**Headless** (`Playwright`, `Puppeteer`) crawlers run a real browser with everything in it. This is for sites that use advanced features like: RSC streaming, service workers, canvas/WebGL that the JavaScript crawlers don't support.

## Wiring

Shared options object (see [CrawlerOptions](./crawler-options.md)):

```csharp
using SimpleCrawler.Core.Browser;

var options = new CrawlerOptions
{
    Concurrency = 16,
    ParseConcurrency = 8,
    MaxPages = 5000,
    BrowserProfile = new DefaultBrowserProfile { UserAgent = "my-crawler/1.0" },
};
```

Swap `BrowserProfile` for `new ChromeBrowserProfile()` to impersonate a real browser — see
[BrowserProfile](./crawler-options.md#browserprofile).

### Static

```csharp
using SimpleCrawler.HtmlAgilityPack;       // or SimpleCrawler.AngleSharp

var services = new ServiceCollection();
services.AddHtmlAgilityPackCrawler(options, (provider, client) =>
    client.DefaultRequestHeaders.Add("Cookie", "session=..."));   // optional HttpClient hook

var crawler = services.BuildServiceProvider().GetRequiredService<ICrawler>();
var result = await crawler.Start("https://example.com/");         // result.Urls = discovered set
```

The `(provider, client)` hook is where custom auth, cookies, and headers go — see
[HttpClient configuration](./httpclient-configuration.md).


### JS (engine)

Register one engine. `dom.js` tokenises the shell HTML into the DOM itself — there is no parser to configure.

```csharp
using SimpleCrawler.Js.V8;                 // engine: or SimpleCrawler.Js.Jint

var renderOptions = new JsRenderOptions { EnableFetch = true };

var services = new ServiceCollection();
services.AddV8Crawler(options, renderOptions);

var crawler = services.BuildServiceProvider().GetRequiredService<ICrawler>();
var result = await crawler.Start("https://example.com/");
```

### Headless

```csharp
using SimpleCrawler.Playwright;            // or SimpleCrawler.Puppeteer

var services = new ServiceCollection();
services.AddPlaywrightCrawler(options);   // no HttpClient hook; configure via browser context

var crawler = services.BuildServiceProvider().GetRequiredService<ICrawler>();
var result = await crawler.Start("https://example.com/");
```
