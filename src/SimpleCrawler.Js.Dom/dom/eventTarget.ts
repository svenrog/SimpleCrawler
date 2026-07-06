export type EventListenerMap = Record<string, Array<(...args: any[]) => void>>;

export function addListener(map: EventListenerMap, type: string, cb: (...args: any[]) => void): void {
    (map[type] ||= []).push(cb);
}

export function removeListener(map: EventListenerMap, type: string, cb: (...args: any[]) => void): void {
    const list = map[type];
    if (!list) return;
    const i = list.indexOf(cb);
    if (i >= 0) list.splice(i, 1);
}

export function fireEvent(target: any, map: EventListenerMap, event: any): boolean {
    const list = map[event.type];
    if (!list || !list.length) return true;
    event.target = target;
    event.currentTarget = target;
    const snapshot = list.slice();
    for (let i = 0; i < snapshot.length; i++) {
        try { snapshot[i](event); } catch { /* a failing listener must not abort dispatch */ }
        if (event._stoppedImmediate) break;
    }
    event.currentTarget = null;
    return !event.defaultPrevented;
}

// The DOM EventTarget base (Node extends it) and a global for bundles that `class X extends EventTarget`.
// The listener map is created on first use so leaf nodes (Text/Comment) that never listen stay allocation-free.
export class EventTarget {
    private _listeners: EventListenerMap | null = null;

    addEventListener(type: string, cb: (...args: any[]) => void): void {
        addListener(this._listeners ||= {}, type, cb);
    }

    removeEventListener(type: string, cb: (...args: any[]) => void): void {
        if (this._listeners) removeListener(this._listeners, type, cb);
    }

    dispatchEvent(event: any): boolean {
        return this._listeners ? fireEvent(this, this._listeners, event) : true;
    }
}
