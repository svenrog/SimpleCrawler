[![Platform](https://img.shields.io/badge/Platform-.NET%2010-blue.svg?style=flat)](https://docs.microsoft.com/en-us/dotnet/)

# Simple crawler project

This project stemmed from the need to crawl a single domain in preparation of load testing, extracting relevant urls along the way. It iteratively grew in size and currently has some useful features.

| Feature    | Support |
| ---------- | ------- |
| Parallel crawling | :heavy_check_mark: |
| Meta robots | :heavy_check_mark: |
| Robots.txt | :heavy_check_mark: |
| Modern .NET features | :heavy_check_mark: |
| Console colors | :rainbow: |

| Integration  | Static responses | Client (js) rendering |
| ---------- | ------- | ------- |
| [HtmlAgilityPack](https://html-agility-pack.net/) | :heavy_check_mark: | :x: |
| [AngleSharp](https://anglesharp.github.io/) | :heavy_check_mark: | :x: |
| [Jint](https://github.com/sebastienros/jint) | :heavy_check_mark: | :grey_exclamation: |
| [Clearscript.V8](https://github.com/ClearFoundry/ClearScript) | :heavy_check_mark: | :grey_exclamation: |
| [Playwright](https://playwright.dev/) | :heavy_check_mark: | :heavy_check_mark: |
| [Puppeteer Sharp](https://www.puppeteersharp.com/) | :heavy_check_mark: | :heavy_check_mark: |
| [Selenium WebDriver](https://www.selenium.dev/) | :skull: | :skull: |

## Running the .exe

Executing the binary will crawl a single domain using the default `HtmlAgilityPack` crawler.

```
smpcrawl -e "<entry url>" -o "<output file>"
```

Full list of possible options can be found [here](./src/SimpleCrawler/Options.cs).

Adjusting which implementation is used can be done by referencing another implementation project and switching service collection extension [here](./src/SimpleCrawler/Extensions/ServiceCollectionExtensions.cs).

## Configuration

Wiring a backend into your own project [`docs/`](./docs/configuration.md):

- [Overview](./docs/configuration.md) - project overview and code examples.
- [CrawlerOptions](./docs/crawler-options.md) - detailed options.
- [HttpClient configuration](./docs/httpclient-configuration.md) - examples of customizations.
- [JavaScript crawlers](./docs/javascript-crawlers.md) - information on javascript crawlers, how to use parsers and options.
- [Performance](./docs/performance.md) - comparison of crawler performance.

## Notes on implementations

### Anglesharp

The Anglesharp maintainers claims the library handles JavaScript with AngleSharp.Js. In reality it only does so on an experimental level, complex libraries like React do not work.

### Selenium WebDriver

This package isn't a good fit for this codebase and is dated. 
There hasn't been any discussions in the Google Groups for years and the maintainers [seem hesitant to implement async](https://groups.google.com/g/selenium-developers/c/nk8IywKyJ08).

## Other things of note

### Console colors

The application uses a [custom log formatter](./src/Logging.Core/CrudeLogFormatter.cs) to set colors in the console.

### Static files from embedded resources

Among the test projects there are [TestHosts](./tests/Crawler.TestHost/Infrastructure/Factories/SpaWebApplicationFactory.cs) capable of serving embedded resources as static files, this makes it possible to start the server entirely from memory. A prerequisite for using it across projects.

### Robots.txt

This implementation is based on the work of Adam Shirt that is found [here](https://github.com/drmathias/robots).
The matching engine has been reworked by me for performance reasons.
A full attributation and license can be found under [`Crawler.Core.Robots`](./src/Crawler.Core/Robots/).