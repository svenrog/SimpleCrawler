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

    // The source of an inline script, as the IDL property rather than the node's text. jQuery's globalEval
    // and every tag manager that injects a snippet assign this one, and an element that treats it as an
    // ordinary expando keeps an empty textContent — so the script that was just written has nothing to run.
    get text(): string {
        return String(this.textContent ?? "");
    }

    set text(value: unknown) {
        this.textContent = value == null ? "" : String(value);
    }

    // The module-support feature test, and the only one a page runs against a *created* element rather than
    // the window: `'noModule' in document.createElement('script')`. An element that does not carry it reads
    // as a pre-2018 browser, and a bundle that branches on it can replace document.body with an
    // "unsupported browser" page — which costs every script that runs after it the whole DOM, not just its
    // own globals. The renderer runs ES modules, so the honest answer is that the property exists.
    get noModule(): boolean {
        return this.hasAttribute("nomodule");
    }

    set noModule(value: unknown) {
        if (value) this.setAttributeInternal("nomodule", "");
        else this.removeAttributeInternal("nomodule");
    }
}
