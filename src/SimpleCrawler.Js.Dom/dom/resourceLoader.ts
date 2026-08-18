import { Event } from "../browser/Event";
import { objectUrlSource } from "../browser/objectUrl";
import { reportSwallowed } from "../diagnostics";

// A <script src> or <link> the bundle appends at runtime (webpack lazy-route JS/CSS chunks, React 18's
// stylesheet loading, analytics loaders). The host drains these between turns: a same-origin <script> is
// fetched and executed so its module registrations run; a <link> is treated as loaded without fetching.
// Either way the node's load event fires, resolving the import() promise the code-split route awaits —
// without it the route never loads and the app sits on its loading fallback (or trips its error boundary).
interface PendingResource {
    id: number;
    node: any;
    // The source of a src the render holds itself (an object URL), read when the node is connected because
    // that is when a browser starts the fetch — the page may revoke the token on the next line.
    held: string | null;
}

// Scripts the HTML parser built rather than page code: everything the fragment parser produces for
// innerHTML/insertAdjacentHTML/DOMParser/<template>, and the shell's own tags. The HTML spec starts such a
// script "already started", so connecting it never runs it — which is why innerHTML is not an XSS vector,
// and why a tag manager that stages its snippets through innerHTML and *also* creates the live ones with
// createElement would otherwise have every staged copy run here, entity-escaped source and all.
// document.write is the exception the spec carves out, and Document.write clears the mark for it.
const _parserInserted = new WeakSet<object>();

export function markParserInserted(node: object): void {
    _parserInserted.add(node);
}

export function clearParserInserted(node: any): void {
    _parserInserted.delete(node);
    const kids = node.childNodes;
    if (kids) for (let i = 0; i < kids.length; i++) clearParserInserted(kids[i]);
}

let _counter = 0;
const _pending: PendingResource[] = [];
const _byId = new Map<number, any>();
const _seen = new WeakSet<object>();

// The types a browser executes a classic or module script for; anything else (application/ld+json, a
// template, Cloudflare Rocket Loader's token-prefixed type) is inert data. Mirrors the shell's own filter.
const runnableTypes = ["", "text/javascript", "module", "application/javascript"];

export function registerResource(node: any): void {
    const tag = node.localName;
    if (tag !== "script" && tag !== "link") return;
    if (tag === "script" && _parserInserted.has(node)) return;
    if (tag === "script" && !node.getAttributeInternal("src")) {
        // An appended script with no src carries its source in the node. A browser runs it the moment it is
        // connected; queuing it here is what makes that true — a tag manager writing its snippet inline, or
        // a loader re-adding a script it took out of the markup, is otherwise silently dead code.
        const type = (node.getAttributeInternal("type") || "").trim().toLowerCase();
        if (runnableTypes.indexOf(type) === -1) return;
        if (!String(node.textContent || "")) return;
    }
    if (_seen.has(node)) return;
    _seen.add(node);
    const id = ++_counter;
    const src = tag === "script" ? node.getAttributeInternal("src") : null;
    _pending.push({ id, node, held: src ? objectUrlSource(String(src)) : null });
    _byId.set(id, node);
}

export function takeResources(): string {
    if (!_pending.length) return "";
    const batch = _pending.splice(0, _pending.length);
    // type carries the script's own type attribute: an appended type="module" has to reach the host's module
    // loader rather than its classic-script entry, or its imports never resolve and its exports never run.
    // text carries a source beside a src only for one the render built: there is nothing to fetch for it.
    return JSON.stringify(batch.map((r) => ({
        id: r.id,
        tag: r.node.localName,
        src: r.node.getAttributeInternal("src") || "",
        type: r.node.getAttributeInternal("type") || "",
        text: r.node.getAttributeInternal("src") ? (r.held || "") : String(r.node.textContent || ""),
    })));
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
        try { handler.call(node, event); } catch (e) { reportSwallowed("resource " + type + " handler", e); }
    }
    if (typeof node.dispatchEvent === "function") {
        try { node.dispatchEvent(event); } catch (e) { reportSwallowed("resource " + type + " dispatch", e); }
    }
}
