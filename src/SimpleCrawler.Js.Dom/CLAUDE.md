# SimpleCrawler.Js.Dom

TypeScript source for the embedded JS preludes: a minimal pure-JS DOM plus browser-API shims that the `SimpleCrawler.Js*` backends execute page scripts against. This project is the only DOM implementation — there is no C# DOM bridge.

## Build (required after any .ts change)

The compiled bundles are committed as embedded resources; editing TypeScript does nothing until you rebuild:

- `npm run build` — all bundles.
- `npm run build:dom` → `../SimpleCrawler.Js/Rendering/Preludes/dom.js`; `build:fetch` → `fetch.js`; `build:indexeddb` → `indexeddb.js`.
- `npm run build:jint` → `../SimpleCrawler.Js.Jint/Preludes/shims.js` (Jint-only engine-compat shims).
- `npm run typecheck` before committing.

## Contract with C#

`crawler/api.ts` exposes the `__crawler*` globals (`__crawlerLoadHtml`, `__crawlerSerialize`, `__crawlerCollectLinks`, `__crawlerPump`, `__crawlerSetViewport`, …) that `JsRenderer` calls. Preserve names and signatures — changing them breaks the C# side silently at runtime.

## Scope boundary

Shim missing browser APIs so SPAs hydrate, but don't reimplement runtimes. Streaming-response rendering (e.g. server components over `ReadableStream`) is out of scope — route such sites to the Playwright/Puppeteer backends instead of growing the shim surface.

Types that page code extends or `instanceof`-checks must be JS classes here (engine host types have no usable prototype chain). Framework code also sniffs for `[native code]` in `toString()` — see `browser/native.ts` (`markPrototypeNative`) when adding globals jQuery-era libraries probe.

## Debugging a site that renders wrong

- Symptom "error-boundary shell + 0/few anchors" almost always means a shim gap threw inside the bundle, not a rendering bug per se.
- Start with `rendersize` from `tests/SimpleCrawler.ProfileRunner` and read the serialized HTML; log fetches and loaded chunks; then slice the failing minified chunk down to the browser API it touches (V8 names stack frames by chunk URL).
- Timers matter: delay-aware task-queue rules live in `scheduler/taskQueue.ts` (long `setTimeout`s are dropped, `clearTimeout` is real) — chunk-load "timeout" errors usually trace here, not to networking.
