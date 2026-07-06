import { HTMLElement } from "./HTMLElement";

// A <dialog> is opened/closed imperatively: components ref the element and call showModal()/close() in a
// mount effect to sync visibility (e.g. `open ? el.showModal() : el.close()`), so the methods must exist or
// the effect throws and trips the SPA error boundary. The layout-less render never shows anything, so these
// only reflect the `open` attribute and record returnValue.
export class HTMLDialogElement extends HTMLElement {
    returnValue = "";

    constructor() {
        super("dialog");
    }

    get open(): boolean {
        return this.hasAttribute("open");
    }

    set open(value: unknown) {
        if (value) this.setAttribute("open", ""); else this.removeAttribute("open");
    }

    show(): void {
        this.setAttribute("open", "");
    }

    showModal(): void {
        this.setAttribute("open", "");
    }

    close(returnValue?: unknown): void {
        this.removeAttribute("open");
        if (returnValue !== undefined) this.returnValue = String(returnValue);
    }
}
