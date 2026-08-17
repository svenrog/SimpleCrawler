// classList as a real class rather than the per-access object literal it used to be: bundles test it by
// identity (`el.classList instanceof DOMTokenList`, `x.constructor === DOMTokenList`) and polyfills patch
// DOMTokenList.prototype to observe class writes — neither of which a literal can satisfy, and naming the
// constructor bare is a ReferenceError that costs the whole script. Instances are held off the element in a
// WeakMap so a node still reports no enumerable own keys (see hideOwnFields) and so repeat reads of
// el.classList return the same object, as in a browser.
const _lists = new WeakMap<object, DOMTokenList>();

export class DOMTokenList {
    private readonly _owner: any;
    private readonly _attribute: string;

    constructor(owner: any, attribute: string) {
        this._owner = owner;
        this._attribute = attribute;
    }

    private _read(): string[] {
        const value = this._owner.getAttribute(this._attribute);
        return (value || "").split(/\s+/).filter(Boolean);
    }

    private _write(tokens: string[]): void {
        this._owner.setAttribute(this._attribute, tokens.join(" "));
    }

    add(...names: string[]): void {
        const tokens = this._read();
        for (const name of names) if (tokens.indexOf(name) < 0) tokens.push(name);
        this._write(tokens);
    }

    remove(...names: string[]): void {
        this._write(this._read().filter((x) => names.indexOf(x) < 0));
    }

    toggle(name: string, force?: boolean): boolean {
        const has = this._read().indexOf(name) >= 0;
        const next = force === undefined ? !has : force;
        if (next && !has) this._write([...this._read(), name]);
        else if (!next && has) this._write(this._read().filter((x) => x !== name));
        return next;
    }

    replace(oldName: string, newName: string): boolean {
        const tokens = this._read();
        const at = tokens.indexOf(oldName);
        if (at < 0) return false;
        tokens[at] = newName;
        this._write(tokens);
        return true;
    }

    contains(name: string): boolean {
        return this._read().indexOf(name) >= 0;
    }

    item(index: number): string | null {
        return this._read()[index] ?? null;
    }

    forEach(callback: (value: string, index: number, parent: DOMTokenList) => void): void {
        const tokens = this._read();
        for (let i = 0; i < tokens.length; i++) callback(tokens[i], i, this);
    }

    // Every token list is a live class attribute here, and no conditional-feature attribute (rel, sandbox)
    // is modelled, so nothing is supported — the spec answer for a list with no defined token set.
    supports(_token: string): boolean {
        return false;
    }

    get length(): number {
        return this._read().length;
    }

    get value(): string {
        return this._read().join(" ");
    }

    set value(v: unknown) {
        this._owner.setAttribute(this._attribute, v == null ? "" : String(v));
    }

    keys(): any {
        return this._read().keys();
    }

    values(): any {
        return this._read().values();
    }

    entries(): any {
        return this._read().entries();
    }

    [Symbol.iterator](): Iterator<string> {
        return this._read()[Symbol.iterator]();
    }

    toString(): string {
        return this._read().join(" ");
    }
}

export function classListFor(owner: object): DOMTokenList {
    let list = _lists.get(owner);
    if (!list) {
        list = new DOMTokenList(owner, "class");
        _lists.set(owner, list);
    }
    return list;
}
