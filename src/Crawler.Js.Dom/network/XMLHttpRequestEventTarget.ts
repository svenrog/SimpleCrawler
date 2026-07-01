type Listener = (event: any) => void;

// Angular/RxJS run XHR through Zone.js, which patches addEventListener on XMLHttpRequestEventTarget.prototype
// (never on XMLHttpRequest directly) and, when scheduling the request's macrotask, reads the original off
// that prototype to attach its own readystatechange listener. Without this base class registered as a global,
// Zone finds no original addEventListener and throws "Cannot read properties of undefined (reading call)"
// before send ever runs. So XHR must be shaped like the browser: an event-target base carrying real
// listener registration, with send dispatching to those listeners (not only the on* handlers).
export class XMLHttpRequestEventTarget {
    private _listeners: Record<string, Listener[]>;

    constructor() {
        this._listeners = {};
    }

    addEventListener(type: string, cb: Listener): void {
        if (typeof cb !== "function") return;
        (this._listeners[type] || (this._listeners[type] = [])).push(cb);
    }

    removeEventListener(type: string, cb: Listener): void {
        const list = this._listeners[type];
        if (!list) return;
        const index = list.indexOf(cb);
        if (index >= 0) list.splice(index, 1);
    }

    dispatchEvent(event: any): boolean {
        const list = event && this._listeners[event.type];
        if (list) {
            for (const cb of list.slice()) {
                try { cb.call(this, event); } catch { /* one failing listener must not abort the rest */ }
            }
        }
        return true;
    }
}
