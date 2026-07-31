# SimpleCrawler.Js

Shared in-process JS rendering backend: `JsRenderer` runs page scripts on a pluggable engine (`SimpleCrawler.Js.Jint` / `SimpleCrawler.Js.V8`) against the embedded preludes in `Rendering/Preludes/` (built from `SimpleCrawler.Js.Dom` — edit the TypeScript there, never these `.js` files).

## Rules

- The renderer path is synchronous: `JsHttp` and `HttpModuleFetcher` call `HttpClient.Send`. Any custom primary `HttpMessageHandler` in the pipeline must override the sync `Send` too, or SPA rendering breaks while plain crawling still works.
- Embed values into generated JS/JSON with `SimpleCrawler.Core.Helpers.JsonLiteral` (Utf8JsonWriter-based, AOT/trim-safe), never `JsonSerializer.Serialize`. Deserializing with `JsonDocument` is fine.
- Caches must assume heterogeneous multi-bundle sites: keep `BoundedLruCache`/`SourceCache` capped — unbounded per-URL caching has caused real memory leaks here.
- Both engines must publish and run under NativeAOT; treat DLR/IL3000 publish warnings as known-benign, but don't add new reflection-dependent code paths.
- **An engine stops itself; nothing outside it can.** Page JS runs synchronously on the calling thread, so the renderer's token reaches it at no `await`, and a stack-depth guard bounds recursion rather than time — a page can hold a core for hours at a near-constant depth. A new engine must honour the token `IJsEngineFactory.Create` hands it *and* carry a wall-clock ceiling, reporting them as `OperationCanceledException` and `TimeoutException`; an engine-native type that escapes untranslated (Jint's `ExecutionCanceledException`, V8's `ScriptInterruptedException`) is swallowed by a caller's "any exception means this render failed" handler, and the cancellation reaches nothing.
- **The ceiling belongs on the engine's own options, named for what that engine can measure it over** — `JintEngineOptions.ScriptTimeout` (per execution call) and `V8EngineOptions.PageTimeout` (per page). One shared setting on `JsRenderOptions` would have to be read as whichever the registered engine happened to mean.
