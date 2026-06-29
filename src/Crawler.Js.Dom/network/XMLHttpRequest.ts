export class XMLHttpRequest {
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
        this.readyState = 0; this.status = 0; this.statusText = ""; this.responseText = ""; this.response = "";
        this._h = {}; this._rh = "{}"; this._method = "GET"; this._url = "";
        this.onreadystatechange = null; this.onload = null; this.onerror = null; this.onloadend = null;
    }
    open(m: string, u: string): void { this._method = m; this._url = u; this.readyState = 1; if (this.onreadystatechange) this.onreadystatechange(); }
    setRequestHeader(k: string, v: any): void { this._h[k] = v; }
    send(body?: any): void {
        const r = __http.request(this._url, this._method, JSON.stringify(this._h), body == null ? null : String(body));
        if (r.error) {
            this.status = 0; this.readyState = 4;
            if (this.onerror) this.onerror(new Error(r.error));
            if (this.onloadend) this.onloadend();
            return;
        }
        this.status = r.status; this.statusText = r.statusText || ""; this.responseText = r.body; this.response = r.body;
        this._rh = r.headersJson || "{}"; this.readyState = 4;
        if (this.onreadystatechange) this.onreadystatechange();
        if (this.onload) this.onload();
        if (this.onloadend) this.onloadend();
    }
    abort(): void { }
    getResponseHeader(n: string): string | null { try { const o = JSON.parse(this._rh); const v = o[n]; return v === undefined ? null : v; } catch (e) { return null; } }
    getAllResponseHeaders(): string { try { const o = JSON.parse(this._rh); let s = ""; for (const k in o) { s += k + ": " + o[k] + "\r\n"; } return s; } catch (e) { return ""; } }
    addEventListener(t: string, cb: any): void {
        if (t === "load") this.onload = cb;
        else if (t === "error") this.onerror = cb;
        else if (t === "loadend") this.onloadend = cb;
        else if (t === "readystatechange") this.onreadystatechange = cb;
    }
    removeEventListener(): void { }
}
