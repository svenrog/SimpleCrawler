import { HTMLElement } from "./HTMLElement";
import { documentRef } from "./documentRef";

// Consent/widget bundles read iframe.contentWindow.postMessage and crash on undefined without it. The
// single-pass render never delivers the message, so postMessage (and the other window methods) are no-ops.
// The frame is same-origin — what an iframe with no src, about:blank or a javascript: src is in a browser —
// so it carries a blank document of its own: a RUM beacon builds its loader by writing into
// `frame.contentWindow.document` and reads `.open()` off it unguarded. Nothing in that document is ever
// executed or serialized.
// Everything the frame's window does *not* define is answered from this realm, because there is only one:
// an accessibility overlay opens a frame precisely to read constructors off it (`win.Node.prototype`), and
// a window carrying a document but no Node is a throw where the missing frame was merely a skipped feature.
// Both halves are cached as non-enumerable props so they keep identity across reads without becoming a
// deep-walk cycle.
export class HTMLIFrameElement extends HTMLElement {
    constructor() {
        super("iframe");
    }

    get src(): string {
        return this.getAttributeInternal("src") || "";
    }

    set src(value: unknown) {
        this.setAttributeInternal("src", value == null ? "" : String(value));
    }

    get contentWindow(): any {
        return (this as any)._contentWindow || this._openFrame().window;
    }

    get contentDocument(): any {
        return (this as any)._contentDocument || this._openFrame().document;
    }

    private _openFrame(): { window: any; document: any } {
        const doc = documentRef.current.implementation.createHTMLDocument("");
        const own: any = {
            document: doc,
            postMessage() { },
            close() { },
            focus() { },
            blur() { },
        };

        const realm: any = globalThis;
        const win = new Proxy(own, {
            get: (target, prop) => (prop in target ? target[prop] : realm[prop]),
            has: (target, prop) => prop in target || prop in realm,
        });

        doc.defaultView = win;
        Object.defineProperty(this, "_contentWindow", { value: win, enumerable: false });
        Object.defineProperty(this, "_contentDocument", { value: doc, enumerable: false });
        return { window: win, document: doc };
    }
}
