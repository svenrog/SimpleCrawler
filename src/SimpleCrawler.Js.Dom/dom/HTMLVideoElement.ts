import { HTMLMediaElement } from "./HTMLMediaElement";

export class HTMLVideoElement extends HTMLMediaElement {
    constructor() {
        super("video");
    }

    get poster(): string {
        return this.getAttributeInternal("poster") || "";
    }

    set poster(value: unknown) {
        this.setAttributeInternal("poster", value == null ? "" : String(value));
    }
}
