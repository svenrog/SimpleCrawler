import { HTMLElement } from "./HTMLElement";

// A page's own bootstrap data is often parked in `<meta content='{...}'>` and read back as a property, not an
// attribute — `JSON.parse(meta.content)`. Without the reflection that read is `JSON.parse(undefined)`, which
// throws during render and takes the whole app down, so nothing the page would have mounted ever runs.
export class HTMLMetaElement extends HTMLElement {
    constructor() {
        super("meta");
    }

    get content(): string {
        return this.getAttributeInternal("content") || "";
    }

    set content(value: unknown) {
        this.setAttributeInternal("content", value == null ? "" : String(value));
    }

    get name(): string {
        return this.getAttributeInternal("name") || "";
    }

    set name(value: unknown) {
        this.setAttributeInternal("name", value == null ? "" : String(value));
    }

    get httpEquiv(): string {
        return this.getAttributeInternal("http-equiv") || "";
    }

    set httpEquiv(value: unknown) {
        this.setAttributeInternal("http-equiv", value == null ? "" : String(value));
    }
}
