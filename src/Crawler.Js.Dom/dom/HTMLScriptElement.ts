import { HTMLElement } from "./HTMLElement";

// webpack's chunk loader assigns `script.src = url` as a property (not setAttribute) and reads it back; the
// pure Element only round-trips attributes, so without this reflection getAttribute("src") stays null and the
// host's resource drain never sees the chunk URL to fetch. type reflects for the same reason (module probes).
export class HTMLScriptElement extends HTMLElement {
    constructor() {
        super("script");
    }

    get src(): string {
        return this.getAttribute("src") || "";
    }

    set src(value: unknown) {
        this.setAttribute("src", value == null ? "" : String(value));
    }

    get type(): string {
        return this.getAttribute("type") || "";
    }

    set type(value: unknown) {
        this.setAttribute("type", value == null ? "" : String(value));
    }
}
