import { HTMLElement } from "./HTMLElement";
import { documentRef } from "./documentRef";

// Consent/widget bundles read iframe.contentWindow.postMessage and crash on undefined without it. The
// single-pass render never delivers the message, so postMessage (and the other window methods) are no-ops.
// contentWindow is cached as a non-enumerable prop so it keeps identity across reads without becoming a
// deep-walk cycle.
// The frame is same-origin — what an iframe with no src, about:blank or a javascript: src is in a browser —
// so it carries a blank document rather than nothing: a RUM beacon builds its loader by writing into
// `frame.contentWindow.document` and reads `.open()` off it unguarded. Nothing in that document is ever
// executed or serialized; it exists so the writer survives.
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
        let win = (this as any)._contentWindow;
        if (!win) {
            const frame = this;
            win = {
                get document() { return frame.contentDocument; },
                postMessage() { },
                close() { },
                focus() { },
                blur() { },
            };
            Object.defineProperty(this, "_contentWindow", { value: win, enumerable: false });
        }
        return win;
    }

    get contentDocument(): any {
        let doc = (this as any)._contentDocument;
        if (!doc) {
            doc = documentRef.current.implementation.createHTMLDocument("");
            Object.defineProperty(this, "_contentDocument", { value: doc, enumerable: false });
        }
        return doc;
    }
}
