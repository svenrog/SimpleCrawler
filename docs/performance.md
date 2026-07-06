# Performance

Full crawl of the 20-route Preact site — every route is a client-only Astro island that hydrates 32
product cards, discovered only after the shell is rendered (i7-10700K, .NET 10, BenchmarkDotNet
ShortRun). Lower is better. This is relative guidance, not a guarantee.

| Engine / backend      | Mean      | Managed alloc |                          |
| --------------------- | ---------:| -------------:| ------------------------ |
| V8                    |    393 ms | 16 MB\*       | fastest                  |
| Jint                  |  4,383 ms | 2,540 MB      | baseline                 |
| Playwright (headless) |  7,502 ms | 60 MB\*       | real Chromium            |
| Puppeteer (headless)  | 15,622 ms | 38 MB\*       | real Chromium            |

\* Understates real memory: V8's heap is native (off the .NET GC) and the headless browsers render in a separate process, so only bridge/marshalling allocations are counted here.

## Guidelines for choosing a crawler

- Start static, climb a tier when links are missed.
- For JS crawling, prefer **V8**. Jint can be used as a fallback when native dependencies can't be installed or the managed heap must be accountable.
- Use headless (Playwright/Puppeteer) only when the pure-JS DOM can't render a site.
- Keep high `Concurrency` but set `ParseConcurrency` to CPU core count on render-heavy crawls
  ([why?](./crawler-options.md#parseconcurrency)).

## Benchmarks

```
dotnet run -c Release --project tests/SimpleCrawler.Benchmarks
```