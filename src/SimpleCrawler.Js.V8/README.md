# SimpleCrawler.Js.V8

A JavaScript-rendering backend for [SimpleCrawler](https://github.com/svenrog/SimpleCrawler) that crawls client-rendered SPAs by executing page scripts on the [ClearScript](https://github.com/microsoft/ClearScript) V8 engine against a pure-JS DOM. It is faster than the Jint backend on heavy pages, at the cost of a native V8 dependency.

**Native V8 binaries for all supported platforms (Windows, Linux, macOS; x64/x86/arm64/arm) are included** — no extra install step. A published, RID-targeted app carries only the native matching its own runtime.

## Installation

```
dotnet add package SimpleCrawler.Js.V8
```

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleCrawler.Core;
using SimpleCrawler.Js.V8;

var services = new ServiceCollection()
    .AddV8Crawler(new CrawlerOptions { MaxPages = 500 })
    .BuildServiceProvider();

var crawler = services.GetRequiredService<ICrawler>();
var result = await crawler.Start("https://example.com");
```

See the [JavaScript crawlers documentation](https://github.com/svenrog/SimpleCrawler/blob/master/docs/javascript-crawlers.md) for details.

## License

MIT — see [LICENSE.txt](https://github.com/svenrog/SimpleCrawler/blob/master/LICENSE.txt).
