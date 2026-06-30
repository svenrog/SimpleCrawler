import { Headers } from "./Headers";

export class Response {
    private _r: any;
    ok: boolean;
    status: number;
    statusText: string;
    url: string;
    redirected: boolean;
    type: string;
    headers: Headers;
    bodyUsed: boolean;
    constructor(r: any) {
        this._r = r; this.ok = !!r.ok; this.status = r.status; this.statusText = r.statusText || "";
        this.url = r.url || ""; this.redirected = false; this.type = "basic";
        let parsed: any = {}; try { parsed = JSON.parse(r.headersJson || "{}"); } catch (e) { }
        this.headers = new Headers(parsed); this.bodyUsed = false;
    }
    text(): Promise<string> { return Promise.resolve(this._r.body || ""); }
    json(): Promise<any> { try { return Promise.resolve(JSON.parse(this._r.body || "null")); } catch (e) { return Promise.reject(e); } }
    clone(): Response { return new Response(this._r); }
}