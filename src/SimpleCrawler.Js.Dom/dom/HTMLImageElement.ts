import { HTMLElement } from "./HTMLElement";

export class HTMLImageElement extends HTMLElement {
    constructor() {
        super("img");
    }

    get alt(): string {
        return this.getAttributeInternal("alt") || "";
    }

    set alt(value: unknown) {
        this.setAttributeInternal("alt", value == null ? "" : String(value));
    }

    get src(): string {
        return this.getAttributeInternal("src") || "";
    }

    set src(value: unknown) {
        this.setAttributeInternal("src", value == null ? "" : String(value));
    }
}