import { HTMLElement } from "./HTMLElement";

export class HTMLImageElement extends HTMLElement {
    constructor() {
        super("img");
    }

    get alt(): string {
        return this.getAttribute("alt") || "";
    }

    set alt(value: unknown) {
        this.setAttribute("alt", value == null ? "" : String(value));
    }

    get src(): string {
        return this.getAttribute("src") || "";
    }

    set src(value: unknown) {
        this.setAttribute("src", value == null ? "" : String(value));
    }
}