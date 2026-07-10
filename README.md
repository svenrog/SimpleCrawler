# Simple crawler

[![Platform](https://img.shields.io/badge/Platform-.NET%2010-blue.svg?style=flat)](https://docs.microsoft.com/en-us/dotnet/)
[![NuGet](https://img.shields.io/nuget/vpre/SimpleCrawler.Core.svg?style=flat&label=SimpleCrawler.Core)](https://www.nuget.org/packages/SimpleCrawler.Core/)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg?style=flat)](./LICENSE.txt)


This project stemmed from the need to crawl a single domain in preparation of load testing, extracting relevant urls along the way. It iteratively grew in size and currently has some useful features.

| Feature    | Support |
| ---------- | ------- |
| Parallel crawling | :heavy_check_mark: |
| Multi-domain crawling | :heavy_check_mark: |
| Meta robots | :heavy_check_mark: |
| Robots.txt | :heavy_check_mark: |
| Browser impersonation | :heavy_check_mark: |
| Proxy pooling | :heavy_check_mark: |
| Retries & backoff | :heavy_check_mark: |
| Checkpoint handling | :heavy_check_mark: |
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

## Installation

The crawler ships as a set of NuGet packages, one per rendering backend, all sharing the `SimpleCrawler.*` prefix. Install the implementation you want (or implement your own):

```
dotnet add package SimpleCrawler.HtmlAgilityPack   # default, static HTML
dotnet add package SimpleCrawler.AngleSharp        # static HTML, AngleSharp
dotnet add package SimpleCrawler.Js.V8             # JS rendering (ClearScript V8)
dotnet add package SimpleCrawler.Js.Jint           # JS rendering (Jint)
dotnet add package SimpleCrawler.Playwright        # headless browser
dotnet add package SimpleCrawler.Puppeteer         # headless browser
```

Wire a backend into your `IServiceCollection` with its `AddXyzCrawler(...)` extension — see each package's README and [docs/](./docs/configuration.md).

> The CLI (`smpcrawl`) is **not** on NuGet; grab the AOT binary from the [GitHub releases](https://github.com/svenrog/SimpleCrawler/releases).

## Running the .exe

Executing the binary will crawl a domain using the default `HtmlAgilityPack` crawler.

```
smpcrawl -e "<entry url>" -o "<output file>"
```
multiple domains are supported too
```
smpcrawl -e "<entry url 1>" -e "<entry url 2>" ...
```

Full list of possible options can be found [here](./src/SimpleCrawler.Console/Options.cs).

Adjusting which implementation is used can be done by referencing another implementation project and switching service collection extension [here](./src/SimpleCrawler.Console/Extensions/ServiceCollectionExtensions.cs).

## Configuration

Project overview and details on how to wire up your own backend are found under [docs/](./docs/configuration.md).

## Notes on implementations

### Anglesharp

The Anglesharp maintainers claims the library handles JavaScript with AngleSharp.Js. In reality it only does so on an experimental level, complex libraries like React do not work. This is why there are two [JavaScript implementations](./docs/javascript-crawlers.md).

### Selenium WebDriver

This package isn't a good fit for this codebase and is dated. 
There hasn't been any discussions in the Google Groups for years and the maintainers [seem hesitant to implement async](https://groups.google.com/g/selenium-developers/c/nk8IywKyJ08).

## Other things of note

### Console colors

The application uses a [custom log formatter](./src/Logging.Core/CrudeLogFormatter.cs) to set colors in the console.

### Static files from embedded resources

Among the test projects there are [TestHosts](./tests/SimpleCrawler.TestHost/Infrastructure/Factories/SpaWebApplicationFactory.cs) capable of serving embedded resources as static files, this makes it possible to start the server entirely from memory. A prerequisite for using it across projects.

### Robots.txt

This implementation is based on the work of Adam Shirt that is found [here](https://github.com/drmathias/robots).
The matching engine has been reworked by me for performance reasons.
A full attributation and license can be found under [`SimpleCrawler.Core.Robots`](./src/SimpleCrawler.Core/Robots/).

## Package maintainer

https://github.com/svenrog

## Change log

Changes are documented in [CHANGELOG.md](./CHANGELOG.md).

