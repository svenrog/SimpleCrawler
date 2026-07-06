import { XMLHttpRequestEventTarget } from "./XMLHttpRequestEventTarget";

export class XMLHttpRequest extends XMLHttpRequestEventTarget {
    static UNSENT = 0;
    static OPENED = 1;
    static HEADERS_RECEIVED = 2;
    static LOADING = 3;
    static DONE = 4;
    readyState: number;
    status: number;
    statusText: string;
    responseText: string;
    response: string;
    onreadystatechange: any;
    onload: any;
    onerror: any;
    onloadend: any;
    private _h: any;
    private _rh: string;
    private _method: string;
    private _url: string;
    constructor() {
        super();
        this.readyState = 0; this.status = 0; this.statusText = ""; this.responseText = ""; this.response = "";
        this._h = {}; this._rh = "{}"; this._method = "GET"; this._url = "";
        this.onreadystatechange = null; this.onload = null; this.onerror = null; this.onloadend = null;
    }
    open(m: string, u: string): void { this._method = m; this._url = u; this.readyState = 1; this._emit("readystatechange"); }
    setRequestHeader(k: string, v: any): void { this._h[k] = v; }
    send(body?: any): void {
        const r = __http.request(this._url, this._method, JSON.stringify(this._h), body == null ? null : String(body));
        if (r.error) {
            this.status = 0; this.readyState = 4;
            this._emit("readystatechange", this.onerror, new Error(r.error));
            this._emit("error", this.onerror, new Error(r.error));
            this._emit("loadend", this.onloadend);
            return;
        }
        this.status = r.status; this.statusText = r.statusText || ""; this.responseText = r.body; this.response = r.body;
        this._rh = r.headersJson || "{}"; this.readyState = 4;
        this._emit("readystatechange");
        this._emit("load", this.onload);
        this._emit("loadend", this.onloadend);
    }
    abort(): void { }
    getResponseHeader(n: string): string | null { try { const o = JSON.parse(this._rh); const v = o[n]; return v === undefined ? null : v; } catch (e) { return null; } }
    getAllResponseHeaders(): string { try { const o = JSON.parse(this._rh); let s = ""; for (const k in o) { s += k + ": " + o[k] + "\r\n"; } return s; } catch (e) { return ""; } }

    // Fire an event to both the corresponding on* handler and any addEventListener listeners (Zone.js
    // attaches its readystatechange listener via the latter). onreadystatechange has no explicit handler
    // argument because it is always read live off `this`.
    private _emit(type: string, handler?: any, arg?: any): void {
        const cb = type === "readystatechange" ? this.onreadystatechange : handler;
        if (typeof cb === "function") { try { cb.call(this, arg); } catch { /* dispatch listeners regardless */ } }
        this.dispatchEvent({ type, target: this });
    }
}
