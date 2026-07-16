import { XMLHttpRequestEventTarget } from "./XMLHttpRequestEventTarget";

// The base-prelude XMLHttpRequest: present so an SDK that patches `XMLHttpRequest.prototype.open` unguarded
// at init (older analytics/fraud scripts do) doesn't throw a ReferenceError, but inert — send() issues no
// request and readyState never advances past OPENED, so a beacon fires into the void. When the fetch shim is
// enabled (EnableFetch), installNetwork replaces this with the functional XHR that goes through __http. This
// keeps the default render's network surface matching the fetch shim's: no request without EnableFetch.
export class XMLHttpRequestStub extends XMLHttpRequestEventTarget {
    static UNSENT = 0;
    static OPENED = 1;
    static HEADERS_RECEIVED = 2;
    static LOADING = 3;
    static DONE = 4;
    readyState = 0;
    status = 0;
    statusText = "";
    responseText = "";
    response = "";
    responseType = "";
    responseURL = "";
    withCredentials = false;
    timeout = 0;
    upload: XMLHttpRequestEventTarget;
    onreadystatechange: any = null;
    onload: any = null;
    onerror: any = null;
    onloadend: any = null;
    onloadstart: any = null;
    onprogress: any = null;
    onabort: any = null;
    ontimeout: any = null;

    constructor() {
        super();
        this.upload = new XMLHttpRequestEventTarget();
    }

    open(): void { this.readyState = 1; }
    setRequestHeader(): void { }
    send(): void { }
    abort(): void { }
    overrideMimeType(): void { }
    getResponseHeader(): string | null { return null; }
    getAllResponseHeaders(): string { return ""; }
}
