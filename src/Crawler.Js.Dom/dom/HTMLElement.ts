import { Element } from "./Element";
import { DocumentFragment } from "./DocumentFragment";
import { customElements } from "./customElements";
import { hideOwnFields } from "./utils";

// The base bundles extend (`class X extends HTMLElement`). It has to be a real JS class — ClearScript
// host types have no prototype, so the C#-bridge mode could only stub it. Here the constructor pulls its
// tag from the registry's name stack when the registry is constructing it, so a subclass `super()` lands
// with the correct localName without the caller passing one.
export class HTMLElement extends Element {
    shadowRoot: any = null;

    constructor(tag?: string, ns?: string) {
        super(tag || customElements.currentName() || "", ns);
        hideOwnFields(this);
        const target = customElements.takeUpgradeTarget();
        if (target) return target;
    }

    attachShadow(init?: { mode?: string }): any {
        if (this.shadowRoot) return this.shadowRoot;
        const root = new DocumentFragment();
        (root as any).host = this;
        (root as any).mode = init && init.mode ? init.mode : "open";
        this.shadowRoot = root;
        return root;
    }

    connectedCallback(): void { }

    disconnectedCallback(): void { }

    adoptedCallback(): void { }

    attributeChangedCallback(_name: string, _oldValue: string | null, _newValue: string | null): void { }

    setAttribute(name: string, value: unknown): void {
        const observed = (this.constructor as any).observedAttributes;
        const tracked = Array.isArray(observed) && observed.indexOf(name) >= 0;
        const old = tracked ? this.getAttribute(name) : null;
        super.setAttribute(name, value);
        if (tracked && typeof this.attributeChangedCallback === "function") {
            this.attributeChangedCallback(name, old, this.getAttribute(name));
        }
    }
}
