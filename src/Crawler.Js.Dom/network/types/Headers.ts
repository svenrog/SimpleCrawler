import { toHeaderObject } from "../utils";

export class Headers {
    private _m: any = {};
    constructor(init?: any) {
        const o = toHeaderObject(init);
        for (const k in o) { this._m[String(k).toLowerCase()] = String(o[k]); }
    }
    get(n: string): string | null { const v = this._m[String(n).toLowerCase()]; return v === undefined ? null : v; }
    has(n: string): boolean { return this._m[String(n).toLowerCase()] !== undefined; }
    set(n: string, v: any): void { this._m[String(n).toLowerCase()] = String(v); }
    append(n: string, v: any): void { const k = String(n).toLowerCase(); this._m[k] = this._m[k] !== undefined ? this._m[k] + ", " + v : String(v); }
    delete(n: string): void { delete this._m[String(n).toLowerCase()]; }
    forEach(cb: (v: string, k: string, parent: Headers) => void): void { for (const k in this._m) { cb(this._m[k], k, this); } }
    keys(): string[] { return Object.keys(this._m); }
}
