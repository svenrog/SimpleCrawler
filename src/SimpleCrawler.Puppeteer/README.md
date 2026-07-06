# SimpleCrawler.Puppeteer

A headless-browser backend for [SimpleCrawler](https://github.com/svenrog/SimpleCrawler) using [PuppeteerSharp](https://www.puppeteersharp.com/). Use it for fully JavaScript-rendered sites that the in-process JS backends can't handle (streaming SSR, complex runtime behaviour).

## Installation

```
dotnet add package SimpleCrawler.Puppeteer
```

PuppeteerSharp **downloads a matching Chromium build on first run** (cached under the user profile), so no separate install step is required — just allow the initial download.

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleCrawler.Core;
using SimpleCrawler.Puppeteer;

var services = new ServiceCollection()
    .AddPuppeteerCrawler(new HeadlessCrawlerOptions { MaxPages = 500 })
    .BuildServiceProvider();

var crawler = services.GetRequiredService<ICrawler>();
var result = await crawler.Start("https://example.com");
```

See the [JavaScript crawlers documentation](https://github.com/svenrog/SimpleCrawler/blob/master/docs/javascript-crawlers.md) for details.

## License

MIT — see [LICENSE.txt](https://github.com/svenrog/SimpleCrawler/blob/master/LICENSE.txt).
