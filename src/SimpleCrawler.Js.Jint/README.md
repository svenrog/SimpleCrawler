# SimpleCrawler.Js.Jint

A JavaScript-rendering backend for [SimpleCrawler](https://github.com/svenrog/SimpleCrawler) that crawls client-rendered SPAs by executing page scripts on the managed [Jint](https://github.com/sebastienros/jint) engine against a pure-JS DOM. Being fully managed, it has **no native dependencies** — a good fit for trimmed/AOT and locked-down environments. For heavier pages, [`SimpleCrawler.Js.V8`](https://www.nuget.org/packages/SimpleCrawler.Js.V8/) is faster.

## Installation

```
dotnet add package SimpleCrawler.Js.Jint
```

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleCrawler.Core;
using SimpleCrawler.Js.Jint;

var services = new ServiceCollection()
    .AddJintCrawler(new CrawlerOptions { MaxPages = 500 })
    .BuildServiceProvider();

var crawler = services.GetRequiredService<ICrawler>();
var result = await crawler.Start("https://example.com");
```

See the [JavaScript crawlers documentation](https://github.com/svenrog/SimpleCrawler/blob/master/docs/javascript-crawlers.md) for details.

## License

MIT — see [LICENSE.txt](https://github.com/svenrog/SimpleCrawler/blob/master/LICENSE.txt).
