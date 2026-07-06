# SimpleCrawler.AngleSharp

A static-HTML backend for [SimpleCrawler](https://github.com/svenrog/SimpleCrawler), using [AngleSharp](https://anglesharp.github.io/)'s standards-compliant HTML parser. Use it when you want AngleSharp's parsing semantics for server-rendered sites; for JavaScript-rendered pages, use a JS or headless backend instead.

## Installation

```
dotnet add package SimpleCrawler.AngleSharp
```

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleCrawler.Core;
using SimpleCrawler.AngleSharp;

var services = new ServiceCollection()
    .AddAngleSharpCrawler(new CrawlerOptions { MaxPages = 500 })
    .BuildServiceProvider();

var crawler = services.GetRequiredService<ICrawler>();
var result = await crawler.Start("https://example.com");
```

See the [documentation](https://github.com/svenrog/SimpleCrawler/tree/master/docs) for configuration and backend details.

## License

MIT — see [LICENSE.txt](https://github.com/svenrog/SimpleCrawler/blob/master/LICENSE.txt).
