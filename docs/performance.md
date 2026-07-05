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

\* Understates real memory: V8's heap is native (off the .NET GC) and the headless browsers render in a
separate process, so only bridge/marshalling allocations are counted here.

V8 renders the whole site **~11× faster than Jint** (~0.39 s vs ~4.4 s) and with two orders of magnitude
less managed garbage. V8 pools one isolate across the crawl; Jint builds a fresh engine per page
([engine reuse](./javascript-crawlers.md#engine-reuse)) — a small fixed cost that pooling was measured
away rather than kept, but the gap here is raw execution speed on a real render workload, not setup. Both
in-process engines beat driving a real browser: Playwright is ~1.7× slower than Jint (~19× slower than
V8) and Puppeteer ~3.6× (~40× V8). Reach for headless only when the pure-JS DOM can't render a site.

The JS crawlers tokenise the shell with `dom.js`; there is no HTML-parser knob. Native pre-parsers
(AngleSharp/HtmlAgilityPack) were benchmarked and removed — on a real render the parse is a rounding error
next to bundle execution, so they moved the total by ~2% (noise) and on V8 allocated *more*, since they
build a managed DOM tree to marshal in. Static backends (no scripting) parse the same shell for a fraction
of the cost; prefer static when links are in the server HTML, and drop to a JS crawler (V8 first) only when
they aren't.

## Rules of thumb

- Start static, climb a tier only when a cheaper one misses links.
- For JS crawling, prefer **V8** — ~11× faster than Jint here and far lighter on the managed heap. Jint
  is the fallback when native dependencies can't be installed or the managed heap must be accountable.
- Use headless (Playwright/Puppeteer) only when the pure-JS DOM can't render a site.
- Keep high `Concurrency` but set `ParseConcurrency` to core count on render-heavy crawls
  ([why?](./crawler-options.md#parseconcurrency)).

## Profiling

Set `JSRENDER_PROFILE=1` to print a per-phase render breakdown (engine create, DOM-shim setup, HTML
parse, bundle execution, task drain) at process exit, summed across pages and threads. Use it to see
where a slow crawl actually spends its time before reaching for a different engine or parser.

`bundle execution` is where a heavy render usually hides, and the phase profiler is blind inside it (the
bundle runs entirely in JS). Set `JSRENDER_DOM_PROFILE=1` to break it down by DOM operation: dom.js counts
the public DOM calls the bundle issues (`insertBefore`, `setAttribute`, `querySelectorAll`, `innerHTML`
sets, …), summed per page host-side. On V8 this also reports exclusive self-time per operation — the run
adds ClearScript's native high-resolution `Performance` clock (~100 ns) only under this flag — so you can
tell a frequent-but-cheap op (`insertBefore`) from a rare-but-expensive one (an `innerHTML` set that
reparses a fragment). Jint has no high-res clock, so it reports counts only. A growing `bundle execution`
usually means the shims now let a bundle hydrate further (more real rendering) rather than a slower engine.

## Run

```
dotnet run -c Release --project tests/Crawler.Benchmarks
```
