# SimpleCrawler.Js.Dom

TypeScript source for the embedded JS preludes: a minimal pure-JS DOM plus browser-API shims that the `SimpleCrawler.Js*` backends execute page scripts against. This project is the only DOM implementation — there is no C# DOM bridge.

## Build (required after any .ts change)

The compiled bundles are committed as embedded resources; editing TypeScript does nothing until you rebuild:

- `npm run build` — all bundles.
- `npm run build:dom` → `../SimpleCrawler.Js/Rendering/Preludes/dom.js`; `build:fetch` → `fetch.js`; `build:indexeddb` → `indexeddb.js`.
- `npm run typecheck` before committing.

## Contract with C#

`crawler/api.ts` exposes the `__crawler*` globals (`__crawlerLoadHtml`, `__crawlerSerialize`, `__crawlerCollectLinks`, `__crawlerPump`, `__crawlerSetViewport`, …) that `JsRenderer` calls. Preserve names and signatures — changing them breaks the C# side silently at runtime.

## Scope boundary

Shim missing browser APIs so SPAs hydrate, but don't reimplement runtimes. The WHATWG Streams surface (`ReadableStream`/`TransformStream`/`TextDecoderStream`/`Response.body`) is now shimmed behind the opt-in `EnableStreams` flag (`stream/` → `stream.js`), but it delivers a **buffered-complete** body — the fetch path already materializes the whole response, so consumers get spec-compliant reader/transform semantics, not incremental transport. Genuinely chunked-over-time / server-push rendering (RSC Flight streamed across many packets) stays out of scope — route those sites to the Playwright/Puppeteer backends instead of growing the shim surface.

Because enabling streams lets a Next.js App Router RSC bundle run its streaming path — which, in a single-pass render, can tear down the server markup and fail to rebuild it — the renderer captures a pre-script baseline (`__crawlerCaptureBaseline` in `crawler/api.ts`) and, at finalize, restores it (`__crawlerGuardRegression`) if the live tree's anchor count regressed below the shell. This guarantees `EnableStreams` never yields fewer links than the server HTML; it does not make such sites render fully. See the `EnableStreams` XML doc in `JsRenderOptions`.

Types that page code extends or `instanceof`-checks must be JS classes here (engine host types have no usable prototype chain). Framework code also sniffs for `[native code]` in `toString()` — see `browser/native.ts` (`markPrototypeNative`) when adding globals jQuery-era libraries probe.

## Debugging a site that renders wrong

- Symptom "error-boundary shell + 0/few anchors" almost always means a shim gap threw inside the bundle, not a rendering bug per se.
- Start with `rendersize` from `tests/SimpleCrawler.ProfileRunner` and read the serialized HTML; log fetches and loaded chunks; then slice the failing minified chunk down to the browser API it touches (V8 names stack frames by chunk URL).
- Timers matter: delay-aware task-queue rules live in `scheduler/taskQueue.ts` (long `setTimeout`s are dropped, `clearTimeout` is real) — chunk-load "timeout" errors usually trace here, not to networking.
