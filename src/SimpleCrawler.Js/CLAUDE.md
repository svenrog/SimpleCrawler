# SimpleCrawler.Js

Shared in-process JS rendering backend: `JsRenderer` runs page scripts on a pluggable engine (`SimpleCrawler.Js.Jint` / `SimpleCrawler.Js.V8`) against the embedded preludes in `Rendering/Preludes/` (built from `SimpleCrawler.Js.Dom` — edit the TypeScript there, never these `.js` files).

## Rules

- The renderer path is synchronous: `JsHttp` and `HttpModuleFetcher` call `HttpClient.Send`. Any custom primary `HttpMessageHandler` in the pipeline must override the sync `Send` too, or SPA rendering breaks while plain crawling still works.
- Embed values into generated JS/JSON with `SimpleCrawler.Core.Helpers.JsonLiteral` (Utf8JsonWriter-based, AOT/trim-safe), never `JsonSerializer.Serialize`. Deserializing with `JsonDocument` is fine.
- Caches must assume heterogeneous multi-bundle sites: keep `BoundedLruCache`/`SourceCache` capped — unbounded per-URL caching has caused real memory leaks here.
- Both engines must publish and run under NativeAOT; treat DLR/IL3000 publish warnings as known-benign, but don't add new reflection-dependent code paths.
