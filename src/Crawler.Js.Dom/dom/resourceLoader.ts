import { Event } from "../browser/Event";

// A <script src> or <link> the bundle appends at runtime (webpack lazy-route JS/CSS chunks, React 18's
// stylesheet loading, analytics loaders). The host drains these between turns: a same-origin <script> is
// fetched and executed so its module registrations run; a <link> is treated as loaded without fetching.
// Either way the node's load event fires, resolving the import() promise the code-split route awaits —
// without it the route never loads and the app sits on its loading fallback (or trips its error boundary).
interface PendingResource {
    id: number;
    node: any;
}

let _counter = 0;
const _pending: PendingResource[] = [];
const _byId = new Map<number, any>();
const _seen = new WeakSet<object>();

export function registerResource(node: any): void {
    const tag = node.localName;
    if (tag !== "script" && tag !== "link") return;
    if (tag === "script" && !node.getAttribute("src")) return;
    if (_seen.has(node)) return;
    _seen.add(node);
    const id = ++_counter;
    _pending.push({ id, node });
    _byId.set(id, node);
}

export function takeResources(): string {
    if (!_pending.length) return "";
    const batch = _pending.splice(0, _pending.length);
    return JSON.stringify(batch.map((r) => ({ id: r.id, tag: r.node.localName, src: r.node.getAttribute("src") || "" })));
}

export function pendingResourceCount(): number {
    return _pending.length;
}

export function fireResourceEvent(id: number, type: string): void {
    const node = _byId.get(id);
    if (!node) return;
    _byId.delete(id);
    const event = new Event(type);
    event.target = node;
    const handler = type === "load" ? node.onload : node.onerror;
    if (typeof handler === "function") {
        try { handler.call(node, event); } catch { /* a failing handler must not abort the drain */ }
    }
    if (typeof node.dispatchEvent === "function") {
        try { node.dispatchEvent(event); } catch { /* same */ }
    }
}
