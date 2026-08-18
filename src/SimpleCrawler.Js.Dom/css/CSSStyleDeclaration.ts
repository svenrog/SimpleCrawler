type StyleStore = Record<string, string>;

// The element whose style attribute this declaration is: reads see what the markup (or a setAttribute) put
// there, writes go back into it. Anything without an owner — getComputedStyle's answer — is a free-standing
// store.
interface StyleOwner {
    getAttributeInternal(name: string): string | null;
    setAttributeInternal(name: string, value: unknown): void;
    removeAttributeInternal(name: string): void;
}

// A declaration a page reads by IDL name and writes by CSS name, over one store. "backgroundColor" and
// "background-color" are the same property, and a vendor spelling folds the same way ("WebkitTransform" →
// "-webkit-transform"); a custom property is its own name and is never folded.
function canonical(name: string): string {
    const text = String(name);
    if (text.slice(0, 2) === "--") return text;
    return text.replace(/[A-Z]/g, (c) => "-" + c.toLowerCase());
}

// Declarations split on ";" — but a value can carry one, inside url(...) or a data: URI, and cutting there
// drops the declaration and everything the page wrote after it.
function declarations(text: string): string[] {
    const out: string[] = [];
    let depth = 0;
    let quote = "";
    let start = 0;
    for (let i = 0; i < text.length; i++) {
        const ch = text[i];
        if (quote) {
            if (ch === quote) quote = "";
            continue;
        }
        if (ch === '"' || ch === "'") quote = ch;
        else if (ch === "(") depth++;
        else if (ch === ")") { if (depth > 0) depth--; }
        else if (ch === ";" && depth === 0) { out.push(text.slice(start, i)); start = i + 1; }
    }
    out.push(text.slice(start));
    return out;
}

function parseCss(text: string, store: StyleStore): void {
    for (const part of declarations(String(text))) {
        const idx = part.indexOf(":");
        if (idx > 0) store[canonical(part.slice(0, idx).trim())] = part.slice(idx + 1).trim();
    }
}

export function createStyleDeclaration(owner?: StyleOwner): any {
    const store: StyleStore = {};
    // What the owner's attribute held the last time this declaration and it agreed. It is the whole
    // synchronisation: a value that differs is a write the page made through setAttribute, and re-parsing on
    // read is what a browser's live declaration does without a notification channel to do it with.
    let attribute: string | null = null;

    const serialize = (): string => {
        const out: string[] = [];
        for (const p in store) if (Object.prototype.hasOwnProperty.call(store, p)) out.push(p + ": " + store[p]);
        return out.join("; ");
    };

    const pull = (): void => {
        if (!owner) return;
        const current = owner.getAttributeInternal("style");
        if (current === attribute) return;
        for (const p in store) if (Object.prototype.hasOwnProperty.call(store, p)) delete store[p];
        if (current) parseCss(current, store);
        attribute = current;
    };

    const push = (): void => {
        if (!owner) return;
        const text = serialize();
        attribute = text || null;
        if (text) owner.setAttributeInternal("style", text);
        else owner.removeAttributeInternal("style");
    };

    const handler: ProxyHandler<Record<string, never>> = {
        get: (_t, k) => {
            if (k === "setProperty") return (n: string, v: string) => { pull(); store[canonical(n)] = String(v); push(); };
            if (k === "removeProperty") return (n: string) => { pull(); delete store[canonical(n)]; push(); };
            if (k === "getPropertyValue") return (n: string) => { pull(); return store[canonical(n)] || ""; };
            if (k === "getPropertyPriority") return () => "";
            if (k === "item") return (i: number) => { pull(); return Object.keys(store)[i] || ""; };
            if (k === "length") { pull(); return Object.keys(store).length; }
            if (k === "cssText") { pull(); return serialize(); }
            if (k === "_store") { pull(); return store; }
            if (typeof k !== "string") return undefined;
            pull();
            const v = store[canonical(k)];
            return v != null ? v : "";
        },
        set: (_t, k, v) => {
            if (typeof k !== "string") return true;
            pull();
            if (k === "cssText") {
                for (const p in store) if (Object.prototype.hasOwnProperty.call(store, p)) delete store[p];
                if (v) parseCss(v, store);
            } else if (v == null || v === "") {
                delete store[canonical(k)];
            } else {
                store[canonical(k)] = String(v);
            }
            push();
            return true;
        },
        // A real style object answers `in` for every CSS property it supports, set or not, and for both the
        // unprefixed and vendor-prefixed spellings. Without a has trap `in` falls through to the bare target
        // and is false for everything — including properties this shim itself just stored — which contradicts
        // the get trap above. The gap is not cosmetic: a prefix probe of the shape
        // `"transform" in style || "WebkitTransform" in style || ...` concludes the property is unsupported,
        // and libraries then use that null as a property name rather than taking a fallback (GSAP's
        // _checkPropPrefix does exactly this, and every subsequent transform read throws). This answers true
        // for unknown names too, where a real browser answers false; that mirrors the get trap, which already
        // returns "" for any key rather than shipping a CSS-property table, and feature probes ask about real
        // (possibly prefixed/future) properties, never deliberate non-properties.
        has: () => true,
        ownKeys: () => { pull(); return Object.keys(store); },
        getOwnPropertyDescriptor: (_t, k) => {
            pull();
            if (typeof k !== "string") return undefined;
            const key = canonical(k);
            if (!Object.prototype.hasOwnProperty.call(store, key)) return undefined;
            return { value: store[key], writable: true, enumerable: true, configurable: true };
        },
    };
    return new Proxy({}, handler);
}
