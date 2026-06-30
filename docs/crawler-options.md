# CrawlerOptions

Source found in `src/Crawler.Core/CrawlerOptions.cs`, controls crawler behaviour.

## Concurrency (default 8)

Fetch workers to run simultaneously. I/O-bound, so can exceed core count; bounded by the target's tolerance and your network, not CPU. Pair high values with [`CrawlDelay`](#crawldelay) when crawling sites you don't own.

## ParseConcurrency (default 0 = match Concurrency)

Parse/render workers to run simultaneously. Parsing, especially when rendering JavaScript, is CPU-bound (having a higher count on this than CPU cores will hurt throughput).
For big page results, use a lower `ParseConcurrency` than `Concurrency`.

## MaxPages (default 10000)

This is a soft cap on pages processed (since threads are in flight, there might be a number of fetch threads additionally on this count). It can limit the crawler from continuing infinitely if a site has quirky query parameter handling that doesn't use canonical urls.

## CrawlDelay (default 0)

Minimum seconds between fetches (global, not per-worker). `0` = no delay. If `RespectRobotsTxt` is on, a `robots.txt` `Crawl-delay` overrides upward.

## RespectRobotsTxt (default true)

Don't just honor `robots.txt`, respect the rules and crawl-delay. Disable it if you dare, I might tell your mother.

## RespectMetaRobots (default true)

Honor `<meta name="robots">`. `noindex` pages are fetched for their links but excluded from results; `nofollow` pages have their links ignored.

## BlockNonEssentialResources (default true)

**When using a headless crawler only.** Aborts images/CSS/fonts/media so the browser loads only what's needed for links. Large bandwidth/time saving, no effect on extraction. Ignored by static/JS crawlers.

## EnableSitemapDiscovery (default true)

Seed from `sitemap.xml` (via `robots.txt`) in addition to following links. Disable to crawl only what's reachable from the entry URL (this is used for testing mainly).

## UserAgent

`User-Agent` header, and the agent matched against `robots.txt`. Set something identifiable.
