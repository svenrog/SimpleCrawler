import { HTMLElement } from "./HTMLElement";

// `<option>` reflects its `value` like the real element (the `value` attribute, falling back to the text
// content) so react-dom's updateOptions can match the select's value against each option and set `selected`.
export class HTMLOptionElement extends HTMLElement {
    constructor() {
        super("option");
    }

    get value(): string {
        const v = this.getAttributeInternal("value");
        return v != null ? v : this.textContent;
    }

    set value(v: unknown) {
        this.setAttributeInternal("value", v == null ? "" : String(v));
    }
}
