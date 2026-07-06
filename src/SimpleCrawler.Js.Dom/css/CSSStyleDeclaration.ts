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
    };
    return new Proxy({}, handler);
}
