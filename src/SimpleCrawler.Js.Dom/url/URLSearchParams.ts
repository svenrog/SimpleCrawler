export class URLSearchParams {
    private pairs: [string, string][] = [];

    constructor(init?: string | URLSearchParams) {
        let src = init;
        if (typeof src === "string" && src.charAt(0) === "?") src = src.slice(1);
        if (typeof src === "string" && src) {
            src.split("&").forEach((p) => {
                if (!p) return;
                const i = p.indexOf("=");
                this.pairs.push(i < 0
                    ? [decodeURIComponent(p), ""]
                    : [decodeURIComponent(p.slice(0, i)), decodeURIComponent(p.slice(i + 1))]);
            });
        }
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
        this.delete(k);
        this.pairs.push([k, String(v)]);
    }

    append(k: string, v: string): void {
        this.pairs.push([k, String(v)]);
    }

    delete(k: string): void {
        this.pairs = this.pairs.filter((p) => p[0] !== k);
    }

    forEach(cb: (value: string, key: string) => void): void {
        this.pairs.forEach((p) => cb(p[1], p[0]));
    }

    entries(): IterableIterator<[string, string]> {
        let i = 0;
        const it = {
            next: (): IteratorResult<[string, string]> =>
                i < this.pairs.length
                    ? { value: this.pairs[i++], done: false }
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
        return this.pairs.map((p) => encodeURIComponent(p[0]) + "=" + encodeURIComponent(p[1])).join("&");
    }

    [Symbol.iterator](): IterableIterator<[string, string]> {
        return this.entries();
    }
}
