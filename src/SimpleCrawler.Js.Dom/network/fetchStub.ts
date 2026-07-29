// The base-prelude fetch: present so a bundle that calls it bare at init — a module shim resolving an import
// map, an SDK loading its own config — doesn't throw a ReferenceError, but inert, rejecting the way a browser
// rejects a request the network refused. Absence and rejection cost the same request; they differ in what
// else survives, because a ReferenceError escapes to the top of the script and every global that script would
// have registered goes with it, while a rejected promise is the case the caller already handles. When the
// fetch shim is enabled (EnableFetch), installNetwork replaces this with the functional fetch that goes
// through __http, so the default render's network surface still matches the shim's: no request without it.
export function fetchStub(): Promise<never> {
    return Promise.reject(new TypeError("Failed to fetch"));
}
