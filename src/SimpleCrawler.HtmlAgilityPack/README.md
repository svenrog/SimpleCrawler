# SimpleCrawler.HtmlAgilityPack

The default static-HTML backend for [SimpleCrawler](https://github.com/svenrog/SimpleCrawler), using [HtmlAgilityPack](https://html-agility-pack.net/). It is the fastest backend and the right choice for server-rendered sites that don't need a JavaScript runtime.

## Installation

```
dotnet add package SimpleCrawler.HtmlAgilityPack
```

## Usage

```csharp
using Microsoft.Extensions.DependencyInjection;
using SimpleCrawler.Core;
using SimpleCrawler.HtmlAgilityPack;

var services = new ServiceCollection()
    .AddHtmlAgilityPackCrawler(new CrawlerOptions { MaxPages = 500 })
    .BuildServiceProvider();

var crawler = services.GetRequiredService<ICrawler>();
var result = await crawler.Start("https://example.com");
```

See the [documentation](https://github.com/svenrog/SimpleCrawler/tree/master/docs) for configuration and backend details.

## License

MIT — see [LICENSE.txt](https://github.com/svenrog/SimpleCrawler/blob/master/LICENSE.txt).
