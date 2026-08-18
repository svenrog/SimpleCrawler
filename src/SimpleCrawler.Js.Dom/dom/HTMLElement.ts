import { Element } from "./Element";
import { customElements } from "./customElements";
import { hideOwnFields } from "./utils";

// A frozen, always-valid ValidityState: no user interaction happens in a single-pass crawl, so every
// field reports valid and framework validation reads no failures.
const ValidityStateAllValid = Object.freeze({
    valueMissing: false,
    typeMismatch: false,
    patternMismatch: false,
    tooLong: false,
    tooShort: false,
    rangeUnderflow: false,
    rangeOverflow: false,
    stepMismatch: false,
    badInput: false,
    customError: false,
    valid: true,
});

// The base bundles extend (`class X extends HTMLElement`). It has to be a real JS class — ClearScript
// host types have no prototype, so the C#-bridge mode could only stub it. Here the constructor pulls its
// tag from the registry's name stack when the registry is constructing it, so a subclass `super()` lands
// with the correct localName without the caller passing one.
export class HTMLElement extends Element {
    constructor(tag?: string, ns?: string) {
        super(tag || customElements.currentName() || "", ns);
        hideOwnFields(this);
        const target = customElements.takeUpgradeTarget();
        if (target) return target;
    }

    focus(): void { }

    blur(): void { }

    // Constraint Validation API. Frameworks grab a form control ref and call setCustomValidity during
    // render, so the methods must exist; they no-op and report valid.
    willValidate: boolean = true;
    validationMessage: string = "";
    get validity(): any { return ValidityStateAllValid; }
    checkValidity(): boolean { return true; }
    reportValidity(): boolean { return true; }
    setCustomValidity(_error: string): void { }

    connectedCallback(): void { }

    disconnectedCallback(): void { }

    adoptedCallback(): void { }

    attributeChangedCallback(_name: string, _oldValue: string | null, _newValue: string | null): void { }

    // Overrides the internal steps rather than the public method, so an observed attribute reports its change
    // however it was set — through setAttribute, or through the reflected property that bypasses it.
    setAttributeInternal(name: string, value: unknown): void {
        const observed = (this.constructor as any).observedAttributes;
        const tracked = Array.isArray(observed) && observed.indexOf(name) >= 0;
        const old = tracked ? this.getAttributeInternal(name) : null;
        super.setAttributeInternal(name, value);
        if (tracked && typeof this.attributeChangedCallback === "function") {
            this.attributeChangedCallback(name, old, this.getAttributeInternal(name));
        }
    }
}
