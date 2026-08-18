import { resolveUrl } from "./resolve";
import { URLSearchParams } from "./URLSearchParams";

// A URL is a live object, not a snapshot: every component is settable and every read re-serializes. A router
// that builds a request as `const u = new URL(base); u.searchParams.set(k, v); fetch(u)` — or that assigns
// `u.pathname` — is the ordinary shape, and against readonly fields it silently sends the URL it started
// with. Nothing throws, so the loss surfaces much later as a response the page did not ask for.
export class URL {
    private _scheme = "";
    private _host = "";
    private _path = "/";
    private _query = "";
    private _fragment = "";
    readonly searchParams: URLSearchParams;

    constructor(url: string, base?: string) {
        this.assign(resolveUrl(url, base));
        this.searchParams = new URLSearchParams(this._query);
        this.searchParams.observe((serialized) => {
            this._query = serialized ? "?" + serialized : "";
        });
    }

    get href(): string {
        return this._scheme + "//" + this._host + this._path + this._query + this._fragment;
    }

    set href(value: unknown) {
        this.assign(resolveUrl(value));
        this.searchParams.reset(this._query);
    }

    get protocol(): string { return this._scheme; }

    set protocol(value: unknown) {
        const scheme = String(value ?? "").replace(/:*$/, "");
        if (/^[a-zA-Z][\w+.-]*$/.test(scheme)) this._scheme = scheme + ":";
    }

    get host(): string { return this._host; }

    set host(value: unknown) {
        const host = String(value ?? "");
        if (host) this._host = host;
    }

    get hostname(): string { return this._host.split(":")[0]; }

    set hostname(value: unknown) {
        const name = String(value ?? "");
        if (name) this._host = this.port ? name + ":" + this.port : name;
    }

    get port(): string { return this._host.split(":")[1] || ""; }

    set port(value: unknown) {
        const port = String(value ?? "");
        this._host = port ? this.hostname + ":" + port : this.hostname;
    }

    get pathname(): string { return this._path; }

    set pathname(value: unknown) {
        const path = String(value ?? "");
        this._path = path.charAt(0) === "/" ? path : "/" + path;
    }

    get search(): string { return this._query; }

    set search(value: unknown) {
        const query = String(value ?? "");
        this._query = !query ? "" : query.charAt(0) === "?" ? query : "?" + query;
        this.searchParams.reset(this._query);
    }

    get hash(): string { return this._fragment; }

    set hash(value: unknown) {
        const hash = String(value ?? "");
        this._fragment = !hash ? "" : hash.charAt(0) === "#" ? hash : "#" + hash;
    }

    get origin(): string { return this._scheme + "//" + this._host; }

    toString(): string {
        return this.href;
    }

    toJSON(): string {
        return this.href;
    }

    private assign(abs: string): void {
        const m = abs.match(/^([a-zA-Z][\w+.-]*:)\/\/([^/?#]*)([^?#]*)(\?[^#]*)?(#.*)?$/) || [];
        this._scheme = m[1] || "";
        this._host = m[2] || "";
        this._path = m[3] || "/";
        this._query = m[4] || "";
        this._fragment = m[5] || "";
    }
}
