import { resolveUrl } from "./resolve";
import { URLSearchParams } from "./URLSearchParams";

export class URL {
    readonly href: string;
    readonly protocol: string;
    readonly host: string;
    readonly hostname: string;
    readonly port: string;
    readonly pathname: string;
    readonly search: string;
    readonly hash: string;
    readonly origin: string;
    readonly searchParams: URLSearchParams;

    constructor(url: string, base?: string) {
        const abs = resolveUrl(url, base);
        const m = abs.match(/^([a-zA-Z][\w+.-]*:)\/\/([^/?#]*)([^?#]*)(\?[^#]*)?(#.*)?$/) || [];
        this.href = abs;
        this.protocol = m[1] || "";
        this.host = m[2] || "";
        this.hostname = (m[2] || "").split(":")[0];
        this.port = (m[2] || "").split(":")[1] || "";
        this.pathname = m[3] || "/";
        this.search = m[4] || "";
        this.hash = m[5] || "";
        this.origin = this.protocol + "//" + this.host;
        this.searchParams = new URLSearchParams(this.search);
    }

    toString(): string {
        return this.href;
    }
}
