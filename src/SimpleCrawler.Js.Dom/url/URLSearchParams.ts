export class URLSearchParams {
    private pairs: [string, string][] = [];
    // Set by the URL that owns this list, so a mutation reaches the URL's own serialization. The spec calls
    // these the update steps; without them `u.searchParams.set(k, v)` writes into an object nothing reads,
    // and the request the page then makes carries the query it had before.
    private onchange: ((serialized: string) => void) | null = null;

    constructor(init?: unknown) {
        this.reset(init);
    }

    get size(): number {
        return this.pairs.length;
    }

    get(k: string): string | null {
        for (const pair of this.pairs) if (pair[0] === k) return pair[1];
        return null;
    }

    getAll(k: string): string[] {
        return this.pairs.filter((p) => p[0] === k).map((p) => p[1]);
    }

    has(k: string): boolean {
        return this.get(k) !== null;
    }

    set(k: string, v: string): void {
        const key = String(k);
        const out: [string, string][] = [];
        let replaced = false;
        for (const pair of this.pairs) {
            if (pair[0] !== key) out.push(pair);
            else if (!replaced) { out.push([key, String(v)]); replaced = true; }
        }
        if (!replaced) out.push([key, String(v)]);
        this.pairs = out;
        this.changed();
    }

    append(k: string, v: string): void {
        this.pairs.push([String(k), String(v)]);
        this.changed();
    }

    delete(k: string): void {
        const key = String(k);
        this.pairs = this.pairs.filter((p) => p[0] !== key);
        this.changed();
    }

    sort(): void {
        this.pairs.sort((a, b) => (a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0));
        this.changed();
    }

    forEach(cb: (value: string, key: string) => void, thisArg?: unknown): void {
        this.pairs.slice().forEach((p) => cb.call(thisArg as any, p[1], p[0]));
    }

    entries(): IterableIterator<[string, string]> {
        let i = 0;
        const snapshot = this.pairs.slice();
        const it = {
            next: (): IteratorResult<[string, string]> =>
                i < snapshot.length
                    ? { value: snapshot[i++], done: false }
                    : { value: undefined as any, done: true },
            [Symbol.iterator]() { return this; },
        };
        return it as IterableIterator<[string, string]>;
    }

    keys(): IterableIterator<string> {
        return this.pairs.map((p) => p[0])[Symbol.iterator]();
    }

    values(): IterableIterator<string> {
        return this.pairs.map((p) => p[1])[Symbol.iterator]();
    }

    toString(): string {
        return this.pairs
            .map((p) => encode(p[0]) + "=" + encode(p[1]))
            .join("&");
    }

    [Symbol.iterator](): IterableIterator<[string, string]> {
        return this.entries();
    }

    // Replaces the whole list without notifying — the owning URL calls this when its own query is assigned.
    reset(init?: unknown): void {
        this.pairs = parseInit(init);
    }

    observe(onchange: (serialized: string) => void): void {
        this.onchange = onchange;
    }

    private changed(): void {
        if (this.onchange) this.onchange(this.toString());
    }
}

// The constructor takes four shapes and a page picks whichever its bundler emitted: a query string, another
// params list, a sequence of [name, value] pairs, and a record. Accepting only the string one means the other
// three serialize to nothing, and the URL a page builds from them silently loses its whole query.
function parseInit(init: unknown): [string, string][] {
    if (init == null) return [];
    if (init instanceof URLSearchParams) return Array.from(init as any) as [string, string][];
    if (typeof init === "string") return parseQuery(init);
    if (typeof (init as any)[Symbol.iterator] === "function") {
        const out: [string, string][] = [];
        for (const entry of init as Iterable<any>) {
            if (entry == null) continue;
            out.push([String(entry[0]), String(entry[1] == null ? "" : entry[1])]);
        }
        return out;
    }
    if (typeof init === "object") {
        return Object.keys(init as object).map(
            (k) => [k, String((init as any)[k] == null ? "" : (init as any)[k])] as [string, string]);
    }
    return parseQuery(String(init));
}

function parseQuery(text: string): [string, string][] {
    const src = text.charAt(0) === "?" ? text.slice(1) : text;
    const out: [string, string][] = [];
    if (!src) return out;
    for (const part of src.split("&")) {
        if (!part) continue;
        const i = part.indexOf("=");
        out.push(i < 0
            ? [decode(part), ""]
            : [decode(part.slice(0, i)), decode(part.slice(i + 1))]);
    }
    return out;
}

// application/x-www-form-urlencoded: a space is "+", and a percent sequence a page hand-built may not be
// decodable — a lone "%" must come back as itself rather than throw out of the constructor.
function decode(text: string): string {
    try {
        return decodeURIComponent(text.replace(/\+/g, " "));
    } catch {
        return text;
    }
}

function encode(text: string): string {
    return encodeURIComponent(text).replace(/%20/g, "+");
}
