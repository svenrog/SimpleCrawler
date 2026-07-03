// Opt-in prelude, run by JsRenderer only when JsRenderOptions.EnableIndexedDb is set. Kept out of dom.js so
// the default render doesn't evaluate the IndexedDB implementation. A site's data cache that feature-detects
// `window.indexedDB` will see it absent by default and take its no-cache path.
import { indexedDB } from "./index";

(globalThis as any).indexedDB = (globalThis as any).indexedDB || indexedDB;
