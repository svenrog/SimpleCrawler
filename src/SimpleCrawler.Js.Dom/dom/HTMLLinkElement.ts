import { HTMLElement } from "./HTMLElement";

// React 18 stylesheet loading and webpack's CSS chunk loader assign `link.rel`/`link.href` as properties; the
// pure Element only round-trips attributes, so reflect them so a runtime-appended <link> serializes with its
// URL and the host's resource drain (which fires the load event without fetching) sees a real link node.
export class HTMLLinkElement extends HTMLElement {
    constructor() {
        super("link");
    }

    get href(): string {
        return this.getAttributeInternal("href") || "";
    }

    set href(value: unknown) {
        this.setAttributeInternal("href", value == null ? "" : String(value));
    }

    get rel(): string {
        return this.getAttributeInternal("rel") || "";
    }

    set rel(value: unknown) {
        this.setAttributeInternal("rel", value == null ? "" : String(value));
    }
}
