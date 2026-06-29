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
| [Jint](https://github.com/sebastienros/jint) | :heavy_check_mark: | :grey_exclamation:[^1] |
| [Clearscript.V8](https://github.com/ClearFoundry/ClearScript) | :heavy_check_mark: | :grey_exclamation:[^1] |
| [Playwright](https://playwright.dev/) | :heavy_check_mark: | :heavy_check_mark: |
| [Puppeteer Sharp](https://www.puppeteersharp.com/) | :heavy_check_mark: | :heavy_check_mark: |
| [Selenium WebDriver](https://www.selenium.dev/) | :skull: | :skull: |

[^1]: Experimental implementation, more details under "Jint and V8" below.

## Running the .exe

Executing the binary will crawl a single domain using the default `HtmlAgilityPack` crawler.

```
smpcrawl -e "<entry url>" -o "<output file>"
```

Full list of possible options can be found [here](./src/SimpleCrawler/Options.cs).

Adjusting which implementation is used can be done by referencing another implementation project and switching service collection extension [here](./src/SimpleCrawler/Extensions/ServiceCollectionExtensions.cs).


## Notes on implementations

### Anglesharp

The Anglesharp maintainers claims the library handles JavaScript with AngleSharp.Js. In reality it only does so on an experimental level, complex libraries like React do not work.

## Jint and V8

Since AngleSharp or any other library lacked the ability to render simpler Javascript sites we added the `Crawler.Js` project that provides a rendering engine. There are 2 engine implementations, `ClearScript.V8` and `Jint`.

Both are tested against the major client-side frameworks and libraries below. The framework bundles are real client-only [Astro](https://astro.build/) islands.

| Framework / library | Jint | ClearScript.V8 |
| ---------- | ------- | ------- |
| [React](https://react.dev/) | :heavy_check_mark: | :heavy_check_mark: |
| [Preact](https://preactjs.com/) | :heavy_check_mark: | :heavy_check_mark: |
| [Solid](https://www.solidjs.com/) | :heavy_check_mark: | :heavy_check_mark: |
| [Svelte](https://svelte.dev/) | :heavy_check_mark: | :heavy_check_mark: |
| [Vue](https://vuejs.org/) | :x:[^2] | :heavy_check_mark: |
| [jQuery](https://jquery.com/) | :heavy_check_mark:[^3] | :heavy_check_mark:[^3] |

[^2]: Vue's `createRenderer` import resolves to a non-callable binding under Jint's ES-module evaluation, so the bundle aborts before mounting. Renders correctly on V8.
[^3]: Covers jQuery's UMD init / feature-detection surface (`createDocumentFragment`, `implementation.createHTMLDocument`, reflected `script.async`/`defer`/`type`) that real bundles touch on load, rather than the full library.

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