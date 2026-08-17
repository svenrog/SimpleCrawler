import { HTMLElement } from "./HTMLElement";
import { URL } from "../url/URL";

// webpack's chunk loader assigns `script.src = url` as a property (not setAttribute) and reads it back; the
// pure Element only round-trips attributes, so without this reflection getAttribute("src") stays null and the
// host's resource drain never sees the chunk URL to fetch. type reflects for the same reason (module probes).
//
// src is a *reflected URL attribute*, and its two halves deliberately disagree (the same split as
// HTMLAnchorElement.href): the setter stores the raw value, so getAttribute("src") returns the literal string
// the markup authored, while the getter resolves it against the document base. Collapsing both onto the
// resolved URL is what a chunk runtime notices — Turbopack derives a chunk's identity by stripping its
// configured base path off getAttribute("src") (`t.startsWith(base) ? t.slice(base.length) : t`), so an
// absolute URL there fails the prefix test and the chunk registers under a key nothing awaits: every chunk
// loads, the entry module's dependency gate never settles, and the app silently never hydrates.
export class HTMLScriptElement extends HTMLElement {
    constructor() {
        super("script");
    }

    get src(): string {
        const raw = this.getAttributeInternal("src");
        if (raw == null) return "";
        try { return new URL(raw).href; } catch { return raw; }
    }

    set src(value: unknown) {
        this.setAttributeInternal("src", value == null ? "" : String(value));
    }

    get type(): string {
        return this.getAttributeInternal("type") || "";
    }

    set type(value: unknown) {
        this.setAttributeInternal("type", value == null ? "" : String(value));
    }
}
