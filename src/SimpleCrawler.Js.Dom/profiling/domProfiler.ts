import { Node } from "../dom/Node";
import { Element } from "../dom/Element";
import { Document } from "../dom/Document";
import { EventTarget } from "../dom/eventTarget";
import { markPrototypeNative } from "../browser/native";

// Opt-in, host-driven (JSRENDER_DOM_PROFILE=1) counter for the DOM operations a bundle drives during
// render. The C#-side profiler only sees engine-boundary calls, so it can't tell whether bundleExec is
// heavy because the interpreter is slow or because the bundle now does far more rendering (deeper hydration
// as the shim surface grew). This counts the public DOM calls the bundle issues, per page, read back and
// summed host-side. Wrappers are installed only when enabled, so a normal render pays nothing.
//
// Counts are the bundle's own calls, approximately: appendChild is intentionally not wrapped (it delegates
// to insertBefore, so every insertion shows as Node.insertBefore); nested internal work inside a wrapped
// call — a chunk of insertBefore calls from an innerHTML parse, matches() from a querySelectorAll — is
// counted when it lands on another wrapped method, which is what "how much rendering" wants to see.

const counts: Record<string, number> = {};
const times: Record<string, number> = {};
let installed = false;

// Timing needs a real high-resolution clock. ClearScript's native Performance.now() (added under
// profiling, ~100ns) qualifies; Jint / plain V8 have only whole-ms Date.now, so timing is left off there
// and only counts are reported. Self-time is exclusive: a call stack subtracts nested wrapped time from
// its parent, so an innerHTML set doesn't also absorb the insertBefore calls it triggers.
const _hostPerf: any = (globalThis as any).Performance;
const _now: (() => number) | null =
    _hostPerf && typeof _hostPerf.now === "function" ? () => _hostPerf.now() : null;

interface Frame { label: string; start: number; child: number; }
const _stack: Frame[] = [];

function record(label: string, fn: () => any): any {
    counts[label] = (counts[label] || 0) + 1;
    if (!_now) return fn();

    const frame: Frame = { label, start: _now(), child: 0 };
    _stack.push(frame);
    try {
        return fn();
    } finally {
        _stack.pop();
        const elapsed = _now() - frame.start;
        times[label] = (times[label] || 0) + (elapsed - frame.child);
        const parent = _stack[_stack.length - 1];
        if (parent) parent.child += elapsed;
    }
}

export function enableDomProfile(): void {
    if (installed) return;
    installed = true;

    wrapMethods(Node, "Node", ["insertBefore", "removeChild", "cloneNode"]);
    wrapMethods(Element, "Element", [
        "setAttribute", "getAttribute", "removeAttribute", "hasAttribute",
        "querySelector", "querySelectorAll", "matches", "closest",
        "getElementsByTagName", "getElementsByClassName",
        "getBoundingClientRect", "getClientRects",
    ]);
    wrapMethods(Document, "Document", [
        "createElement", "createElementNS", "createTextNode", "createComment",
        "createDocumentFragment", "createRange", "getElementById",
        "getElementsByTagName", "getElementsByClassName", "querySelector", "querySelectorAll",
    ]);
    wrapMethods(EventTarget, "EventTarget", ["addEventListener", "removeEventListener", "dispatchEvent"]);
    wrapSetter(Element, "innerHTML", "Element.set innerHTML");
    wrapSetter(Element, "textContent", "Element.set textContent");

    // Wrapping replaces the methods, so re-stamp the prototypes as native-looking or a bundle's
    // `[native code]` feature-detection (jQuery) would fall off its fast path under profiling.
    for (const ctor of [Node, Element, Document, EventTarget]) markPrototypeNative(ctor);
}

export function dumpDomProfile(): string {
    return installed ? JSON.stringify({ counts, timesMs: _now ? times : null }) : "";
}

function wrapMethods(ctor: any, group: string, names: string[]): void {
    const proto = ctor && ctor.prototype;
    if (!proto) return;
    for (const name of names) {
        const orig = proto[name];
        if (typeof orig !== "function") continue;
        const label = group + "." + name;
        proto[name] = function (this: any, ...args: any[]): any {
            return record(label, () => orig.apply(this, args));
        };
    }
}

function wrapSetter(ctor: any, prop: string, label: string): void {
    const proto = ctor && ctor.prototype;
    if (!proto) return;
    const desc = Object.getOwnPropertyDescriptor(proto, prop);
    if (!desc || typeof desc.set !== "function") return;
    const origSet = desc.set;
    desc.set = function (this: any, v: any): void {
        record(label, () => { origSet.call(this, v); });
    };
    Object.defineProperty(proto, prop, desc);
}
