# SimpleCrawler.Js

The shared JavaScript-rendering pipeline for [SimpleCrawler](https://github.com/svenrog/SimpleCrawler): a pure-JS DOM prelude plus the renderer that executes page scripts to crawl client-rendered SPAs without a full browser.

You don't install this package directly — install one of the engine backends, which reference it transitively:

- [`SimpleCrawler.Js.Jint`](https://www.nuget.org/packages/SimpleCrawler.Js.Jint/) — managed [Jint](https://github.com/sebastienros/jint) engine, no native dependencies
- [`SimpleCrawler.Js.V8`](https://www.nuget.org/packages/SimpleCrawler.Js.V8/) — [ClearScript](https://github.com/microsoft/ClearScript) V8 engine, faster on heavy pages

For sites that need a real browser (streaming SSR, complex runtime features), use [`SimpleCrawler.Playwright`](https://www.nuget.org/packages/SimpleCrawler.Playwright/) or [`SimpleCrawler.Puppeteer`](https://www.nuget.org/packages/SimpleCrawler.Puppeteer/) instead.

## Installation

```
dotnet add package SimpleCrawler.Js
```

## License

MIT — see [LICENSE.txt](https://github.com/svenrog/SimpleCrawler/blob/master/LICENSE.txt).
