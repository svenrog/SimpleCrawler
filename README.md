[![Platform](https://img.shields.io/badge/Platform-.NET%2010-blue.svg?style=flat)](https://docs.microsoft.com/en-us/dotnet/)

# Simple crawler project

This project stemmed from the need to crawl a single site for the purpose of load testing it. Extracting relevant urls along the way. It iteratively grew in size and currently has some useful features.

| Feature    | Support |
| ---------- | ------- |
| Parallel crawling | :100: |
| Meta robots | :heavy_check_mark: |
| Robots.txt | :heavy_check_mark: |

| Integration  | Static responses | Client (js) rendering |
| ---------- | ------- | ------- |
| [HtmlAgilityPack](https://html-agility-pack.net/) | :heavy_check_mark: | :x: |
| [AngleSharp](https://anglesharp.github.io/) | :heavy_check_mark: | :poop: |
| [Playwright](https://playwright.dev/) | :heavy_check_mark: | :heavy_check_mark: |

## Running the .exe

Executing the binary will crawl a single domain using the default `HtmlAgilityPack` crawler.

```
smpcrawl -e "<entry url>" -o "<output file>"
```

Full list of possible options can be found [here](./src/SimpleCrawler/Options.cs)
Adjusting which implementation is used can be done by referencing another implementation project and switching service collection extension [here](./src/SimpleCrawler/Extensions/ServiceCollectionExtensions.cs).

## Some things of note

Among the test projects there are [TestHosts](./tests/Crawler.TestHost/Infrastructure/Factories/SpaWebApplicationFactory.cs) capable of serving embedded resources as static files, this makes it possible to start the server entirely from memory. A prerequisite for using it across projects.

## A note on Robots.txt

This implementation is based on the work of Adam Shirt that is found [here](https://github.com/drmathias/robots).
The matching engine has been reworked by me for performance reasons.
A full attributation and license can be found under [`Crawler.Core.Robots`](./src/Crawler.Core/Robots/).