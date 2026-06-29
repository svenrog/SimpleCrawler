import { HTMLElement } from "./HTMLElement";
import { URL } from "../url/URL";

// `a` reflects its href: the `href` *property* round-trips through the `href` attribute (the setter stores the
// raw value, so getAttribute("href") — and the crawler's link extractor — still see it), while the getter and
// the URL-component accessors resolve it against the document base. Routers/helmets assign
// `anchor.href = "/path"` and read back `protocol`/`host`/`pathname`; without reflection those were plain
// expandos and the components came back undefined.
export class HTMLAnchorElement extends HTMLElement {
    constructor() {
        super("a");
    }

    get href(): string {
        const raw = this.getAttribute("href");
        if (raw == null) return "";
        try { return new URL(raw).href; } catch { return raw; }
    }

    set href(value: unknown) {
        this.setAttribute("href", value == null ? "" : String(value));
    }

    private resolved(): URL | null {
        const raw = this.getAttribute("href");
        if (!raw) return null;
        try { return new URL(raw); } catch { return null; }
    }

    get protocol(): string { return this.resolved()?.protocol ?? ""; }
    get host(): string { return this.resolved()?.host ?? ""; }
    get hostname(): string { return this.resolved()?.hostname ?? ""; }
    get port(): string { return this.resolved()?.port ?? ""; }
    get pathname(): string { return this.resolved()?.pathname ?? ""; }
    get search(): string { return this.resolved()?.search ?? ""; }
    get hash(): string { return this.resolved()?.hash ?? ""; }
    get origin(): string { return this.resolved()?.origin ?? ""; }
}
