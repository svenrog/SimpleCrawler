import type { Document } from "./Document";
import type { Element } from "./Element";
import { NodeType } from "../types/NodeType";

interface Definition {
    readonly ctor: any;
    readonly extendsTag: string | null;
}

// Custom-element registry. `define` retroactively upgrades elements the parser already created as plain
// Elements (the Astro island case: <astro-island> is in the server-rendered HTML long before the bundle
// registers it), and `tryCreate` hands `createElement` a real subclass instance so framework field
// initializers run. Upgrade can't re-run a class constructor on an existing object, so it swaps the
// prototype and fires the connected callback — sufficient for components that read state in
// connectedCallback rather than the constructor.
export class CustomElementRegistry {
    private readonly _definitions = new Map<string, Definition>();
    private readonly _pending = new Map<string, Array<(ctor: any) => void>>();
    private readonly _nameStack: string[] = [];
    private _doc: Document | null = null;

    setDocument(doc: Document): void {
        this._doc = doc;
    }

    define(name: unknown, ctor: any, options?: { extends?: string }): void {
        const tag = String(name).toLowerCase();
        if (this._definitions.has(tag)) return;
        const extendsTag = options && options.extends ? String(options.extends).toLowerCase() : null;
        this._definitions.set(tag, { ctor, extendsTag });
        if (this._doc) this._upgradeSubtree((this._doc as any).documentElement);
        const waiters = this._pending.get(tag);
        if (waiters) {
            this._pending.delete(tag);
            for (const w of waiters) w(ctor);
        }
    }

    get(name: unknown): any {
        const def = this._definitions.get(String(name).toLowerCase());
        return def ? def.ctor : undefined;
    }

    whenDefined(name: unknown): Promise<any> {
        const tag = String(name).toLowerCase();
        const def = this._definitions.get(tag);
        if (def) return Promise.resolve(def.ctor);
        return new Promise<any>((resolve) => {
            const arr = this._pending.get(tag) || [];
            arr.push(resolve);
            this._pending.set(tag, arr);
        });
    }

    upgrade(root: unknown): void {
        if (root) this._upgradeSubtree(root);
    }

    // createElement path: construct a fresh instance with the registry-supplied tag on the name stack so a
    // subclass `super()` lands on HTMLElement with the right localName. null for unregistered names.
    tryCreate(name: string): Element | null {
        const def = this._definitions.get(name);
        if (!def) return null;
        this._nameStack.push(name);
        try {
            return new def.ctor() as Element;
        } finally {
            this._nameStack.pop();
        }
    }

    currentName(): string | undefined {
        return this._nameStack[this._nameStack.length - 1];
    }

    private _upgradeSubtree(root: unknown): void {
        const stack: any[] = [root];
        while (stack.length) {
            const n = stack.pop();
            if (!n) continue;
            if (n.nodeType === NodeType.Element) {
                const def = this._definitions.get(n.localName);
                if (def) this._upgradeOne(n, def.ctor);
            }
            const kids = n.childNodes;
            if (kids) for (let i = 0; i < kids.length; i++) stack.push(kids[i]);
        }
    }

    private _upgradeOne(el: any, ctor: any): void {
        if (ctor && el instanceof ctor) return;
        if (ctor) Object.setPrototypeOf(el, ctor.prototype);
        if (typeof el.connectedCallback === "function" && el.isConnected) {
            el._connected = true;
            el.connectedCallback();
        }
    }
}

export const customElements = new CustomElementRegistry();
