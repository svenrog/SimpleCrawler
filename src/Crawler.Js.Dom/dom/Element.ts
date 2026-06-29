import { Node } from "./Node";
import { NodeType } from "../types/NodeType";
import { Text } from "./Text";
import { createStyleDeclaration } from "../css/CSSStyleDeclaration";
import { collectByTag, textOf } from "./utils";
import { querySelectorAll } from "../selector/querySelector";
import { serializeChildren, serializeNode } from "../html/serializer";

export class Element extends Node {
    localName: string;
    tagName: string;
    nodeName: string;
    readonly namespaceURI: string;
    readonly style: any;

    private readonly attrs = new Map<string, string>();
    private readonly listeners: Record<string, Array<(...args: any[]) => void>> = {};
    cachedInnerHTML: string | null = null;
    private _sheet: any = null;

    constructor(tag: string, ns?: string) {
        super(NodeType.Element);
        this.localName = String(tag).toLowerCase();
        this.tagName = this.localName.toUpperCase();
        this.nodeName = this.tagName;
        this.namespaceURI = ns || "http://www.w3.org/1999/xhtml";
        this.style = createStyleDeclaration();
    }

    setAttribute(name: string, value: unknown): void {
        this.attrs.set(name, value == null ? "" : String(value));
    }

    setAttributeNS(_ns: string | null, name: string, value: unknown): void {
        this.setAttribute(name, value);
    }

    getAttribute(name: string): string | null {
        return this.attrs.has(name) ? this.attrs.get(name)! : null;
    }

    removeAttribute(name: string): void {
        this.attrs.delete(name);
    }

    removeAttributeNS(_ns: string | null, name: string): void {
        this.attrs.delete(name);
    }

    hasAttribute(name: string): boolean {
        return this.attrs.has(name);
    }

    getAttributeNames(): string[] {
        return Array.from(this.attrs.keys());
    }

    addEventListener(t: string, cb: (...args: any[]) => void): void {
        (this.listeners[t] ||= []).push(cb);
    }

    removeEventListener(): void { }
    dispatchEvent(): boolean { return true; }
    setAttributeNode(): void { }

    getElementsByTagName(tag: string): Element[] {
        const out: Node[] = [];
        collectByTag(this, String(tag).toLowerCase(), out);
        return out as unknown as Element[];
    }

    querySelector(sel: string): Element | null {
        const r = querySelectorAll(this, sel);
        return r.length ? (r[0] as Element) : null;
    }

    querySelectorAll(sel: string): Element[] {
        return querySelectorAll(this, sel) as unknown as Element[];
    }

    closest(): Element | null { return null; }

    getBoundingClientRect(): any {
        return { top: 0, left: 0, right: 0, bottom: 0, width: 0, height: 0, x: 0, y: 0 };
    }

    contains(n: Node | null): boolean {
        let cur: Node | null = n;
        while (cur) {
            if (cur === this) return true;
            cur = cur.parentNode;
        }
        return false;
    }

    get relList(): any {
        return {
            supports: () => true,
            add: () => { },
            remove: () => { },
            toggle: () => false,
            contains: () => false,
        };
    }

    appendChild(child: Node): Node {
        this.cachedInnerHTML = null;
        return super.appendChild(child);
    }

    insertBefore(child: Node, ref: Node | null): Node {
        this.cachedInnerHTML = null;
        const wasFrag = child.nodeType === NodeType.DocumentFragment;
        const fragKids = wasFrag ? child.childNodes.slice() : null;
        const r = super.insertBefore(child, ref);
        if (this.isConnected) {
            if (fragKids) for (const k of fragKids) this._notifyConnected(k);
            else this._notifyConnected(child);
        }
        return r;
    }

    removeChild(child: Node): Node {
        const wasConnected = (child as any).isConnected;
        this.cachedInnerHTML = null;
        const r = super.removeChild(child);
        if (wasConnected) this._notifyDisconnected(child);
        return r;
    }

    get isConnected(): boolean {
        let n: Node | null = this.parentNode;
        while (n) {
            if (n.nodeType === NodeType.Document) return true;
            n = n.parentNode;
        }
        return false;
    }

    private _notifyConnected(node: Node): void {
        if (node.nodeType === NodeType.Element) {
            const el = node as any;
            if (!el._connected && typeof el.connectedCallback === "function" && el.isConnected) {
                el._connected = true;
                el.connectedCallback();
            }
        }
        const kids = node.childNodes;
        for (let i = 0; i < kids.length; i++) this._notifyConnected(kids[i]);
    }

    private _notifyDisconnected(node: Node): void {
        if (node.nodeType === NodeType.Element) {
            const el = node as any;
            if (el._connected && typeof el.disconnectedCallback === "function") {
                el._connected = false;
                el.disconnectedCallback();
            }
        }
        const kids = node.childNodes;
        for (let i = 0; i < kids.length; i++) this._notifyDisconnected(kids[i]);
    }

    get id(): string {
        return this.attrs.get("id") || "";
    }
    set id(v: unknown) {
        this.attrs.set("id", String(v));
    }

    get className(): string {
        return this.attrs.get("class") || "";
    }
    set className(v: unknown) {
        this.attrs.set("class", String(v));
    }

    get children(): Element[] {
        return this.childNodes.filter((n) => n.nodeType === NodeType.Element) as unknown as Element[];
    }

    get innerHTML(): string {
        return this.cachedInnerHTML != null ? this.cachedInnerHTML : serializeChildren(this);
    }

    set innerHTML(v: unknown) {
        this.childNodes = [];
        this.cachedInnerHTML = v == null ? "" : String(v);
    }

    get textContent(): string {
        return textOf(this);
    }

    set textContent(v: unknown) {
        this.childNodes = [];
        this.cachedInnerHTML = null;
        if (v != null && v !== "") this.appendChild(new Text(v));
    }

    get outerHTML(): string {
        return serializeNode(this);
    }

    get sheet(): any {
        if (this.localName !== "style") return null;
        if (!this._sheet) {
            const rules: any[] = [];
            this._sheet = {
                cssRules: rules,
                rules,
                ownerNode: this,
                insertRule: (rule: string, index?: number) => {
                    const i = index == null ? rules.length : index;
                    rules.splice(i, 0, { cssText: rule });
                    return i;
                },
                deleteRule: (index: number) => { rules.splice(index, 1); },
            };
        }
        return this._sheet;
    }
}
