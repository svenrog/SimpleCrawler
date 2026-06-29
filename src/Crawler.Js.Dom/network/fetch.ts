// Synchronous networking bridge over the host __http.request call. fetch returns an already-resolved
// Promise so .then()/await chains settle on the existing microtask drain without Task<->Promise bridging.
// Opt-in only: JsRenderOptions.EnableFetch, since it issues live HTTP requests.

import { Response } from "./Response";
import { toHeaderObject } from "./utils";

export function fetch(input: any, init?: any): Promise<Response> {
    init = init || {};
    // A URL host object stringifies to "[object Object]" under V8, so read its href explicitly.
    if (input && typeof input === "object" && typeof input.href === "string" && typeof input.url !== "string") input = input.href;
    let url: string, method: string, headers: any, body: any;
    if (input && typeof input === "object" && "url" in input) {
        url = input.url; method = init.method || input.method || "GET";
        headers = init.headers || input.headers; body = init.body !== undefined ? init.body : input.body;
    } else {
        url = String(input); method = init.method || "GET"; headers = init.headers; body = init.body;
    }
    const r = __http.request(url, method, JSON.stringify(toHeaderObject(headers)), body == null ? null : String(body));
    if (r.error) return Promise.reject(new TypeError(r.error));
    return Promise.resolve(new Response(r));
}