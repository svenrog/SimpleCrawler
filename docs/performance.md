# Performance

One full render pass of the `SpaRenderBenchmarks` site (client-only Preact Astro island; i7-10700K, .NET 10). Lower is better. This is relative guidance, not a guarantee.

| Engine × parser        | Mean   | Managed alloc |                            |
| ---------------------- | ------:| -------------:| -------------------------- |
| Jint + JS tokenizer    | 554 ms | 208 MB        | baseline                   |
| Jint + AngleSharp      | 487 ms | 167 MB        |                            |
| Jint + HtmlAgilityPack | 460 ms | 162 MB        | fastest / lowest-alloc Jint |
| V8 + JS tokenizer      | 232 ms | 11 MB\*       |                            |
| V8 + AngleSharp        | 209 ms | 28 MB\*       |                            |
| V8 + HtmlAgilityPack   | 206 ms | 24 MB\*       | fastest, ~2.7× baseline    |

\* V8 allocations understate real memory, the heap is native, off the .NET GC.

Both engines pool render engines across the crawl ([engine pooling](./javascript-crawlers.md#engine-pooling)).
Jint's pool reuses the realm and skips the per-page DOM-shim setup, which roughly halved the Jint rows
above versus reconstructing the shim every page — so the two engines are now ~2–2.7× apart rather than ~4×.

Static backends parse the same page in low tens of ms / ~30 MB, no scripting. Prefer static when links are in the server HTML.
Drop to JS (V8 + HtmlAgilityPack, or Jint + HtmlAgilityPack) when not.
Use headless only when the shim can't render.

The HTML parser is a real lever on both engines. Because pooling amortises the per-page DOM-shim setup that
used to dominate a Jint render, HTML parsing is again a meaningful share of the time — HtmlAgilityPack is
~15–20% faster than the JS tokenizer on Jint (it was a wash before pooling), and on V8 it shaves ~10% and
cuts allocations ~2×. Prefer V8 for JS crawling; Jint is the fallback when native dependencies can't be
installed or the managed heap must be accountable.

## Rules of thumb

- Start static, climb a tier only when a cheaper one misses links.
- When using a JS crawler: register HtmlAgilityPack. It is the fastest, lowest-allocation option on
  both engines now that pooling makes the parser a meaningful share of a Jint render.
- High `Concurrency` but set `ParseConcurrency` to core count on render-heavy crawls
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
