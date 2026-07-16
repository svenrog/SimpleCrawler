type StyleStore = Record<string, string>;

function parseCss(text: string, store: StyleStore): void {
    const parts = String(text).split(";");
    for (const part of parts) {
        const idx = part.indexOf(":");
        if (idx > 0) store[part.slice(0, idx).trim()] = part.slice(idx + 1).trim();
    }
}

export function createStyleDeclaration(): any {
    const store: StyleStore = {};
    const handler: ProxyHandler<Record<string, never>> = {
        get: (_t, k) => {
            if (k === "setProperty") return (n: string, v: string) => { store[n] = v; };
            if (k === "removeProperty") return (n: string) => { delete store[n]; };
            if (k === "getPropertyValue") return (n: string) => store[n] || "";
            if (k === "cssText") {
                const out: string[] = [];
                for (const p in store) if (Object.prototype.hasOwnProperty.call(store, p)) out.push(p + ": " + store[p]);
                return out.join("; ");
            }
            if (k === "_store") return store;
            const v = store[k as string];
            return v != null ? v : "";
        },
        set: (_t, k, v) => {
            if (k === "cssText") {
                for (const p in store) delete store[p];
                if (v) parseCss(v, store);
                return true;
            }
            store[k as string] = v;
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
    };
    return new Proxy({}, handler);
}
