# SimpleCrawler.Core

The engine-agnostic core of [SimpleCrawler](https://github.com/svenrog/SimpleCrawler) — a high-performance, single-domain web crawler for gathering URLs (originally built for load testing). It handles parallel crawling, `robots.txt`/sitemap parsing, and meta-robots, and defines the pluggable HTML/rendering backend abstractions.

You rarely install this package directly — pick a backend package instead, which pulls `SimpleCrawler.Core` in transitively:

- [`SimpleCrawler.HtmlAgilityPack`](https://www.nuget.org/packages/SimpleCrawler.HtmlAgilityPack/) — static HTML (default)
- [`SimpleCrawler.AngleSharp`](https://www.nuget.org/packages/SimpleCrawler.AngleSharp/) — static HTML
- [`SimpleCrawler.Js.Jint`](https://www.nuget.org/packages/SimpleCrawler.Js.Jint/) / [`SimpleCrawler.Js.V8`](https://www.nuget.org/packages/SimpleCrawler.Js.V8/) — JavaScript rendering
- [`SimpleCrawler.Playwright`](https://www.nuget.org/packages/SimpleCrawler.Playwright/) / [`SimpleCrawler.Puppeteer`](https://www.nuget.org/packages/SimpleCrawler.Puppeteer/) — headless browsers

## Installation

```
dotnet add package SimpleCrawler.Core
```

## Third-party notices

The `robots.txt`/sitemap parser under `SimpleCrawler.Core.Robots` is based on [drmathias/robots](https://github.com/drmathias/robots) (MIT, © 2023 Adam Shirt), reworked for performance. The full license ships in the package as `THIRD-PARTY-NOTICES.txt`.

## License

MIT — see [LICENSE.txt](https://github.com/svenrog/SimpleCrawler/blob/master/LICENSE.txt).
