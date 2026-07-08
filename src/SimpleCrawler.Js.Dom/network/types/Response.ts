import { Headers } from "./Headers";
import { TextEncoder } from "../../browser/TextEncoder";

export class Response {
    private _bodyText: string;
    private _bodyStream: any;
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
        this._bodyStream = undefined;
        this.status = init.status === undefined ? 200 : init.status;
        this.ok = this.status >= 200 && this.status < 300;
        this.statusText = init.statusText || "";
        this.url = "";
        this.redirected = false;
        this.type = "default";
        this.headers = init.headers instanceof Headers ? init.headers : new Headers(init.headers);
        this.bodyUsed = false;
    }
    // Exposes the buffered body as a ReadableStream only when the Streams shim (EnableStreams) is
    // installed; otherwise null, as in a browser without a stream body. Cached so repeat access returns
    // the same stream (spec) and reflects bodyUsed once read.
    get body(): any {
        const g: any = globalThis as any;
        if (typeof g.ReadableStream !== "function") return null;
        if (this._bodyStream === undefined) {
            const bytes = new TextEncoder().encode(this._bodyText);
            this._bodyStream = new g.ReadableStream({
                start: (controller: any) => {
                    if (bytes.length) controller.enqueue(bytes);
                    controller.close();
                    this.bodyUsed = true;
                },
            });
        }
        return this._bodyStream;
    }
    text(): Promise<string> { this.bodyUsed = true; return Promise.resolve(this._bodyText); }
    json(): Promise<any> { try { return Promise.resolve(JSON.parse(this._bodyText || "null")); } catch (e) { return Promise.reject(e); } }
    arrayBuffer(): Promise<ArrayBuffer> { this.bodyUsed = true; return Promise.resolve(new TextEncoder().encode(this._bodyText).buffer as ArrayBuffer); }
    clone(): Response {
        const c = new Response(this._bodyText, { status: this.status, statusText: this.statusText, headers: this.headers });
        c.ok = this.ok; c.url = this.url; c.type = this.type; c.redirected = this.redirected;
        return c;
    }
}
