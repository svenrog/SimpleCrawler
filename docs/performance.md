# Performance

Per-page-render, JS tier, from `SpaRenderBenchmarks` (client-only Preact Astro island; i7-10700K, .NET 10).
Lower is better. Relative guidance, not guarantees.

| Engine × parser        | Mean   | Managed alloc |                          |
| ---------------------- | ------:| -------------:| ------------------------ |
| Jint + JS tokenizer    | 721 ms | 295 MB        | baseline                 |
| Jint + AngleSharp      | 617 ms | 252 MB        |                          |
| Jint + HtmlAgilityPack | 302 ms | 129 MB        | best Jint, ~2.4× baseline |
| V8 + JS tokenizer      | 246 ms | 9 MB\*        |                          |
| V8 + AngleSharp        | 218 ms | 27 MB\*       |                          |
| V8 + HtmlAgilityPack   | 112 ms | 20 MB\*       | fastest, ~6.4× baseline  |

\* V8 allocations understate real memory, the heap is native, off the .NET GC.

Static backends parse the same page in low tens of ms / ~30 MB, no scripting. Prefer static when links are in the server HTML.
Drop to JS (V8 + HtmlAgilityPack, or Jint + HtmlAgilityPack for AOT) when not.
Use headless only when the shim can't render.

## Rules of thumb

- Start static, climb a tier only when a cheaper one misses links.
- When using a JS crawler: always register HtmlAgilityPack, roughly halves render time on both engines.
- V8 for speed, Jint for AOT/zero native deps.
- High `Concurrency` but set `ParseConcurrency` to core count on render-heavy crawls
  ([why?](./crawler-options.md#parseconcurrency)).

## Run

```
dotnet run -c Release --project tests/Crawler.Benchmarks
```
