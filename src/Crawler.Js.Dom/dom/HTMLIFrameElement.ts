import { HTMLElement } from "./HTMLElement";

// A cross-origin iframe only exposes postMessage on its contentWindow; consent/widget bundles read
// iframe.contentWindow.postMessage and crash on undefined without it. The single-pass render never
// delivers the message, so postMessage (and the other window methods) are no-ops. contentWindow is
// cached as a non-enumerable prop so it keeps identity across reads without becoming a deep-walk cycle.
export class HTMLIFrameElement extends HTMLElement {
    constructor() {
        super("iframe");
    }

    get src(): string {
        return this.getAttribute("src") || "";
    }

    set src(value: unknown) {
        this.setAttribute("src", value == null ? "" : String(value));
    }

    get contentWindow(): any {
        let win = (this as any)._contentWindow;
        if (!win) {
            win = {
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
        return null;
    }
}
