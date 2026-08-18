import { HTMLElement } from "./HTMLElement";
import { documentRef } from "./documentRef";

// A control's `value` is a property, not an attribute read: the value attribute only seeds it, and a page
// that finds a field and measures `field.value.length` gets undefined off a plain Element and throws there.
// Nothing types into a single-pass render, so the seeded value is the whole story.
export class HTMLInputElement extends HTMLElement {
    private _value: string | null = null;
    private _checked: boolean | null = null;

    constructor(tag?: string) {
        super(tag || "input");
    }

    get value(): string {
        if (this._value !== null) return this._value;
        return this.getAttributeInternal("value") ?? "";
    }

    set value(v: unknown) {
        this._value = v == null ? "" : String(v);
    }

    get defaultValue(): string {
        return this.getAttributeInternal("value") ?? "";
    }

    set defaultValue(v: unknown) {
        this.setAttributeInternal("value", v == null ? "" : String(v));
    }

    get checked(): boolean {
        return this._checked !== null ? this._checked : this.hasAttribute("checked");
    }

    set checked(v: unknown) {
        this._checked = !!v;
    }

    get defaultChecked(): boolean {
        return this.hasAttribute("checked");
    }

    set defaultChecked(v: unknown) {
        if (v) this.setAttributeInternal("checked", "");
        else this.removeAttributeInternal("checked");
    }

    get type(): string {
        return (this.getAttributeInternal("type") ?? "text").toLowerCase();
    }

    set type(v: unknown) {
        this.setAttributeInternal("type", v == null ? "" : String(v));
    }

    get name(): string {
        return this.getAttributeInternal("name") ?? "";
    }

    set name(v: unknown) {
        this.setAttributeInternal("name", v == null ? "" : String(v));
    }

    get disabled(): boolean {
        return this.hasAttribute("disabled");
    }

    set disabled(v: unknown) {
        if (v) this.setAttributeInternal("disabled", "");
        else this.removeAttributeInternal("disabled");
    }

    // The form this control submits with: the one its owner attribute names, else the nearest ancestor form.
    // Page code retargets a search box with `input.form.action = …`, which needs the element, not null.
    get form(): any {
        const owner = this.getAttributeInternal("form");
        if (owner) return documentRef.current ? documentRef.current.getElementById(owner) : null;
        for (let n: any = this.parentNode; n; n = n.parentNode) {
            if (n.localName === "form") return n;
        }
        return null;
    }

    get placeholder(): string {
        return this.getAttributeInternal("placeholder") ?? "";
    }

    set placeholder(v: unknown) {
        this.setAttributeInternal("placeholder", v == null ? "" : String(v));
    }

    select(): void { }

    setSelectionRange(): void { }
}
