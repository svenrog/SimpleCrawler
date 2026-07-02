import { Headers } from "./Headers";

export class Response {
    private _bodyText: string;
    ok: boolean;
    status: number;
    statusText: string;
    url: string;
    redirected: boolean;
    type: string;
    headers: Headers;
    bodyUsed: boolean;
    constructor(body?: any, init?: any) {
        init = init || {};
        this._bodyText = body == null ? "" : String(body);
        this.status = init.status === undefined ? 200 : init.status;
        this.ok = this.status >= 200 && this.status < 300;
        this.statusText = init.statusText || "";
        this.url = "";
        this.redirected = false;
        this.type = "default";
        this.headers = init.headers instanceof Headers ? init.headers : new Headers(init.headers);
        this.bodyUsed = false;
    }
    text(): Promise<string> { this.bodyUsed = true; return Promise.resolve(this._bodyText); }
    json(): Promise<any> { try { return Promise.resolve(JSON.parse(this._bodyText || "null")); } catch (e) { return Promise.reject(e); } }
    clone(): Response {
        const c = new Response(this._bodyText, { status: this.status, statusText: this.statusText, headers: this.headers });
        c.ok = this.ok; c.url = this.url; c.type = this.type; c.redirected = this.redirected;
        return c;
    }
}
