import { HTMLElement } from "./HTMLElement";
import { URL } from "../url/URL";

// `form` reflects the attributes page code retargets it through — a site rewrites its search box's endpoint
// with `input.form.action = '/search/'`, which has to land on the attribute rather than on an expando the
// serialized tree never sees. Like an anchor's href, the getter resolves against the document base while the
// setter stores the raw value.
export class HTMLFormElement extends HTMLElement {
    constructor() {
        super("form");
    }

    get action(): string {
        const raw = this.getAttributeInternal("action");
        if (raw == null) return "";
        try { return new URL(raw).href; } catch { return raw; }
    }

    set action(value: unknown) {
        this.setAttributeInternal("action", value == null ? "" : String(value));
    }

    get method(): string {
        return (this.getAttributeInternal("method") ?? "get").toLowerCase();
    }

    set method(value: unknown) {
        this.setAttributeInternal("method", value == null ? "" : String(value));
    }

    get name(): string {
        return this.getAttributeInternal("name") ?? "";
    }

    set name(value: unknown) {
        this.setAttributeInternal("name", value == null ? "" : String(value));
    }

    get elements(): any[] {
        return this.querySelectorAll("input, select, textarea, button");
    }

    // Nothing navigates in a single-pass render; the methods exist so a submit handler's own call does not
    // throw partway through the work it does around it.
    submit(): void { }

    requestSubmit(): void { }

    reset(): void { }
}
