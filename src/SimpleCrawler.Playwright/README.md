# SimpleCrawler.Playwright

A headless-browser backend for [SimpleCrawler](https://github.com/svenrog/SimpleCrawler) using [Microsoft Playwright](https://playwright.dev/). Use it for fully JavaScript-rendered sites that the in-process JS backends can't handle (streaming SSR, complex runtime behaviour).

## Installation

```
dotnet add package SimpleCrawler.Playwright
```

Playwright needs its browser binaries. After building, install them once:

```
pwsh bin/Debug/net10.0/playwright.ps1 install chromium --with-deps
```

(See the [Playwright .NET docs](https://playwright.dev/dotnet/docs/browsers) for CI and cross-platform variants.)

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleCrawler.Core;
using SimpleCrawler.Playwright;

var services = new ServiceCollection()
    .AddPlaywrightCrawler(new HeadlessCrawlerOptions { MaxPages = 500 })
    .BuildServiceProvider();

var crawler = services.GetRequiredService<ICrawler>();
var result = await crawler.Start("https://example.com");
```

See the [JavaScript crawlers documentation](https://github.com/svenrog/SimpleCrawler/blob/master/docs/javascript-crawlers.md) for details.

## License

MIT — see [LICENSE.txt](https://github.com/svenrog/SimpleCrawler/blob/master/LICENSE.txt).
