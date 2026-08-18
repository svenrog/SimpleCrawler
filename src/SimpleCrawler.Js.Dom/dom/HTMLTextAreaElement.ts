import { HTMLInputElement } from "./HTMLInputElement";

// Same value semantics as the other controls, except the seed is the element's text rather than an attribute.
export class HTMLTextAreaElement extends HTMLInputElement {
    constructor() {
        super("textarea");
    }

    get value(): string {
        const own = super.value;
        return own !== "" ? own : this.textContent;
    }

    set value(v: unknown) {
        super.value = v;
    }
}
