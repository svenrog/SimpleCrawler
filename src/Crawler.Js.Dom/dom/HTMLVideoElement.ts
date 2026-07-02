import { HTMLMediaElement } from "./HTMLMediaElement";

export class HTMLVideoElement extends HTMLMediaElement {
    constructor() {
        super("video");
    }

    get poster(): string {
        return this.getAttribute("poster") || "";
    }

    set poster(value: unknown) {
        this.setAttribute("poster", value == null ? "" : String(value));
    }
}
