import { fetch } from "./fetch";
import { FormData } from "./types/FormData";
import { Headers } from "./types/Headers";
import { Request } from "./types/Request";
import { Response } from "./types/Response";
import { XMLHttpRequest } from "./XMLHttpRequest";
import { XMLHttpRequestEventTarget } from "./XMLHttpRequestEventTarget";

export function installNetwork(global: any): void {
    // The host embeds __httpRequest as a variadic function returning the response as a JSON string; wrap it so
    // the fetch/XHR shims keep calling __http.request(...) and receive a parsed object. Keeping __http a pure
    // JS object (not a host object) sidesteps ClearScript V8's NativeAOT limitation on host-method invocation.
    global.__http = global.__http || {
        request: (url: string, method: string, headersJson: string, body: string | null) =>
            JSON.parse(__httpRequest(url, method, headersJson, body)),
    };
    global.Headers = global.Headers || Headers;
    global.Response = global.Response || Response;
    global.Request = global.Request || Request;
    global.FormData = global.FormData || FormData;
    // Override the base prelude's inert stubs: with the fetch shim enabled, both fetch and XHR issue real
    // requests through __http. Assigned unconditionally (not `||`) so the functional ones win over the stubs
    // the base prelude always installs, and the XHR pair together so both come from this bundle — Zone.js
    // patches the event-target prototype the functional XHR extends.
    global.fetch = fetch;
    global.XMLHttpRequest = XMLHttpRequest;
    global.XMLHttpRequestEventTarget = XMLHttpRequestEventTarget;
}