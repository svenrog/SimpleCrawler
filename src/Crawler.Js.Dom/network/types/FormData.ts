export class FormData {
    private _e: [string, any][] = [];

    append(name: string, value: any): void { this._e.push([String(name), value]); }

    delete(name: string): void { const n = String(name); this._e = this._e.filter(p => p[0] !== n); }

    get(name: string): any { const n = String(name); for (const p of this._e) if (p[0] === n) return p[1]; return null; }

    getAll(name: string): any[] { const n = String(name); const out: any[] = []; for (const p of this._e) if (p[0] === n) out.push(p[1]); return out; }

    has(name: string): boolean { const n = String(name); for (const p of this._e) if (p[0] === n) return true; return false; }

    set(name: string, value: any): void {
        const n = String(name); let added = false; const out: [string, any][] = [];
        for (const p of this._e) { if (p[0] === n) { if (!added) { out.push([n, value]); added = true; } } else out.push(p); }
        if (!added) out.push([n, value]); this._e = out;
    }

    entries(): Iterator<[string, any]> {
        let i = 0; const d = this._e;
        return { next() { return i < d.length ? { value: d[i++], done: false } : { value: undefined, done: true }; } };
    }

    keys(): Iterator<string> {
        let i = 0; const d = this._e;
        return { next() { return i < d.length ? { value: d[i++][0], done: false } : { value: undefined, done: true }; } };
    }

    values(): Iterator<any> {
        let i = 0; const d = this._e;
        return { next() { return i < d.length ? { value: d[i++][1], done: false } : { value: undefined, done: true }; } };
    }

    forEach(cb: (value: any, key: string, parent: FormData) => void, thisArg?: any): void {
        for (const p of this._e) cb.call(thisArg, p[1], p[0], this);
    }
}

(FormData.prototype as any)[Symbol.iterator] = FormData.prototype.entries;
