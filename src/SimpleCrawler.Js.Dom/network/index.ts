// Synchronous networking bridge over the host __http.request call. fetch returns an already-resolved
// Promise so .then()/await chains settle on the existing microtask drain without Task<->Promise bridging.
// Opt-in only: JsRenderOptions.EnableFetch, since it issues live HTTP requests.

import { installNetwork } from "./api";

installNetwork(globalThis as any);