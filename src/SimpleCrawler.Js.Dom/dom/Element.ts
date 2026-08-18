import { Node } from "./Node";
import { NodeType } from "../types/NodeType";
import { Text } from "./Text";
import { createStyleDeclaration } from "../css/CSSStyleDeclaration";
import { collectByTag, collectByClass, textOf, hideOwnFields } from "./utils";
import { querySelectorAll, matchesSelector } from "../selector/querySelector";
import { serializeChildren, serializeNode } from "../html/serializer";
import { parserRef } from "../html/parserRef";
import { registerResource } from "./resourceLoader";
import { DOMTokenList, classListFor } from "./DOMTokenList";
import { viewportWidth, viewportHeight } from "../browser/viewport";
import { Animatable } from "./Animatable";
import { Animation } from "./Animation";
import { HTMLCollection } from "./HTMLCollection";

function attrNode(name: string, value: string, owner: Element): any {
    return { name, value, localName: name, namespaceURI: null, ownerElement: owner };
}

function nthAttrNode(attrs: Map<string, string>, index: number, owner: Element): any {
    let i = 0;
    for (const [name, value] of attrs) {
        if (i++ === index) return attrNode(name, value, owner);
    }
    return undefined;
}

export class Element extends Node implements Animatable {
    localName: string;
    tagName: string;
    nodeName: string;
    readonly namespaceURI: string;
    readonly style: any;

    private readonly attrs = new Map<string, string>();
    cachedInnerHTML: string | null = null;
    private _sheet: any = null;

    constructor(tag: string, ns?: string) {
        super(NodeType.Element);
        this.localName = String(tag).toLowerCase();
        this.tagName = this.localName.toUpperCase();
        this.nodeName = this.tagName;
        this.namespaceURI = ns || "http://www.w3.org/1999/xhtml";
        this.style = createStyleDeclaration();
        hideOwnFields(this);
    }

    // The attribute steps a browser runs inside the platform, where page code cannot reach them. Every
    // reflected IDL property (el.src, el.href, …) goes through these rather than through the public methods
    // below: a consent blocker that wraps Element.prototype.setAttribute and, inside its wrapper, assigns the
    // property it is guarding re-enters its own wrapper otherwise, and that recursion spends the whole stack
    // before the page has run. A browser is immune because its setter never calls the method.
    setAttributeInternal(name: string, value: unknown): void {
        this.attrs.set(name, value == null ? "" : String(value));
    }

    getAttributeInternal(name: string): string | null {
        return this.attrs.has(name) ? this.attrs.get(name)! : null;
    }

    removeAttributeInternal(name: string): void {
        this.attrs.delete(name);
    }

    setAttribute(name: string, value: unknown): void {
        this.setAttributeInternal(name, value);
    }

    setAttributeNS(_ns: string | null, name: string, value: unknown): void {
        this.setAttributeInternal(name, value);
    }

    getAttribute(name: string): string | null {
        return this.getAttributeInternal(name);
    }

    removeAttribute(name: string): void {
        this.removeAttributeInternal(name);
    }

    removeAttributeNS(_ns: string | null, name: string): void {
        this.attrs.delete(name);
    }

    hasAttribute(name: string): boolean {
        return this.attrs.has(name);
    }

    toggleAttribute(name: string, force?: boolean): boolean {
        const present = this.attrs.has(name);
        const add = force === undefined ? !present : force;
        if (add) {
            if (!present) this.attrs.set(name, "");
            return true;
        }
        this.attrs.delete(name);
        return false;
    }

    getAttributeNames(): string[] {
        return Array.from(this.attrs.keys());
    }

    // A live NamedNodeMap-ish view: custom-element upgrade code walks `el.attributes` by index reading
    // `.length`/`[i].name`/`[i].value`, and React's singleton-attribute teardown does
    // `for (c = el.attributes; c.length;) el.removeAttributeNode(c[0])` — that loop only terminates if
    // `.length` is read live off the current attribute set, not snapshotted once when `.attributes` is accessed.
    // A NamedNodeMap also exposes its attributes as named properties, which is how a jQuery-era feature
    // detection reads one back after setting it (`div.attributes[name].expando`); it dereferences the
    // result, so an undefined there throws during the library's own init.
    get attributes(): any {
        const el = this;
        return new Proxy({}, {
            get(_t, prop) {
                if (prop === "length") return el.attrs.size;
                if (prop === "item") return (i: number) => nthAttrNode(el.attrs, i, el);
                if (prop === "getNamedItem") return (name: string) => el.getAttributeNode(name);
                // A NamedNodeMap is iterable, and a widget copying an element's attributes spreads it
                // (`[...el.attributes]`); answering undefined here is "is not iterable" thrown at the trap.
                if (prop === Symbol.iterator) {
                    return function* () {
                        for (const name of Array.from(el.attrs.keys())) yield el.getAttributeNode(name);
                    };
                }
                if (typeof prop !== "string") return undefined;
                if (/^\d+$/.test(prop)) return nthAttrNode(el.attrs, Number(prop), el);
                return el.attrs.has(prop) ? attrNode(prop, el.attrs.get(prop)!, el) : undefined;
            },
        });
    }

    getAttributeNode(name: string): any {
        return this.attrs.has(name) ? attrNode(name, this.attrs.get(name)!, this) : null;
    }

    setAttributeNode(attr: any): any {
        if (attr && attr.name != null) this.attrs.set(String(attr.name), attr.value == null ? "" : String(attr.value));
        return null;
    }

    removeAttributeNode(attr: any): any {
        if (attr && attr.name != null) this.attrs.delete(String(attr.name));
        return attr;
    }

    getElementsByTagName(tag: string): Element[] {
        const out = new HTMLCollection<Node>();
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

    matches(sel: string): boolean {
        return matchesSelector(this, sel);
    }

    closest(sel: string): Element | null {
        let cur: Node | null = this;
        while (cur) {
            if (cur.nodeType === NodeType.Element && matchesSelector(cur as any, sel)) return cur as unknown as Element;
            cur = cur.parentNode;
        }
        return null;
    }

    getBoundingClientRect(): any {
        return { top: 0, left: 0, right: 0, bottom: 0, width: 0, height: 0, x: 0, y: 0 };
    }

    // Element-level scroll no-ops, mirroring the window shims in browser/scroll.ts. The single-pass render
    // never scrolls, but a banner/nav component calls element.scrollTo({left,behavior}) during its init
    // (e.g. OneTrust/Astro islands), and a missing method throws and trips the surrounding error boundary.
    scrollTo(): void { }
    scrollBy(): void { }
    scroll(): void { }
    scrollIntoView(): void { }

    // jQuery gates .offset()/visibility on `getClientRects().length` before reading the box: a connected
    // element has one (zero-sized) rect, a detached one has none — matching the browser so the "is this laid
    // out?" branch takes the attached path instead of throwing on a missing method.
    getClientRects(): any[] {
        return this.isConnected ? [this.getBoundingClientRect()] : [];
    }

    // The viewport-sized box: jQuery's $(window).width() and many breakpoint helpers read the root element's
    // clientWidth/Height rather than window.innerWidth. Only the root (html/body) reports the viewport; every
    // other element is unlaid-out and reports 0, as in the always-zero getBoundingClientRect.
    get clientWidth(): number {
        return this.localName === "html" || this.localName === "body" ? viewportWidth() : 0;
    }

    get clientHeight(): number {
        return this.localName === "html" || this.localName === "body" ? viewportHeight() : 0;
    }

    // Unlike client* (0 for non-root), offset* must never be 0 or undefined here: layout-driven components
    // size themselves by dividing a container measurement by an element's offsetWidth (marquees duplicating
    // content to fill a row, virtualized lists computing an item count). A 0 or undefined denominator makes
    // that ratio NaN/Infinity, so the ensuing `new Array(count)` throws "Invalid array length" and trips the
    // SPA error boundary. A nonzero viewport-sized stand-in keeps the ratio finite (and small).
    get offsetWidth(): number {
        return viewportWidth();
    }

    get offsetHeight(): number {
        return viewportHeight();
    }

    get offsetTop(): number {
        return 0;
    }

    get offsetLeft(): number {
        return 0;
    }

    // null terminates the `while (el = el.offsetParent)` offset-accumulation idiom; a non-null value loops forever.
    get offsetParent(): Element | null {
        return null;
    }

    // Web Animations: unlaid-out elements never animate, but the returned Animation is used synchronously
    // (cancel/play/pause, onfinish, currentTime) and animation-sequencing code awaits `.finished`/`.ready`
    // then calls `.commitStyles()`, so a missing member throws inside the effect that a finite offsetWidth now
    // lets run. Hand back an inert, already-settled Animation. Each call gets its own resolved promises.
    animate(): any {
        return {
            currentTime: 0,
            startTime: null,
            playState: "finished",
            playbackRate: 1,
            pending: false,
            effect: null,
            timeline: null,
            id: "",
            finished: Promise.resolve(),
            ready: Promise.resolve(),
            onfinish: null,
            oncancel: null,
            play() { },
            pause() { },
            cancel() { },
            finish() { },
            reverse() { },
            persist() { },
            commitStyles() { },
            updatePlaybackRate() { },
            addEventListener() { },
            removeEventListener() { },
        };
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
            registerResource(el);
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

    get dataset(): any {
        const attrs = this.attrs;
        const key = (p: string) => "data-" + p.replace(/[A-Z]/g, (m) => "-" + m.toLowerCase());
        return new Proxy({}, {
            get(_t, p) {
                if (typeof p !== "string") return undefined;
                const v = attrs.get(key(p));
                return v == null ? undefined : v;
            },
            set(_t, p, value) {
                if (typeof p === "string") attrs.set(key(p), String(value));
                return true;
            },
            has(_t, p) {
                return typeof p === "string" && attrs.has(key(p));
            },
        });
    }

    protected _shallowClone(): Node {
        const el = new Element(this.localName, this.namespaceURI);
        for (const [k, v] of this.attrs) el.attrs.set(k, v);
        return el;
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

    get dir(): string {
        return this.attrs.get("dir") || "";
    }
    set dir(v: unknown) {
        this.attrs.set("dir", String(v));
    }

    get lang(): string {
        return this.attrs.get("lang") || "";
    }
    set lang(v: unknown) {
        this.attrs.set("lang", String(v));
    }

    get classList(): DOMTokenList {
        return classListFor(this);
    }

    get children(): Element[] {
        const out = new HTMLCollection<Node>();
        for (const n of this.childNodes) if (n.nodeType === NodeType.Element) out.push(n);
        return out as unknown as Element[];
    }

    get childElementCount(): number {
        return this.children.length;
    }

    // Element-only traversal. Slider/drag libraries step through slides via nextElementSibling and cache
    // the track's parentElement/firstElementChild; a missing accessor returns undefined where they expect an
    // element-or-null, so the next `.removeAttribute`/`.classList` call throws instead of skipping.
    get parentElement(): Element | null {
        const p = this.parentNode;
        return p && p.nodeType === NodeType.Element ? (p as unknown as Element) : null;
    }

    get firstElementChild(): Element | null {
        return this.children[0] || null;
    }

    get lastElementChild(): Element | null {
        const kids = this.children;
        return kids[kids.length - 1] || null;
    }

    get nextElementSibling(): Element | null {
        let n = this.nextSibling;
        while (n && n.nodeType !== NodeType.Element) n = n.nextSibling;
        return (n as unknown as Element) || null;
    }

    get previousElementSibling(): Element | null {
        let n = this.previousSibling;
        while (n && n.nodeType !== NodeType.Element) n = n.previousSibling;
        return (n as unknown as Element) || null;
    }

    getElementsByClassName(className: string): Element[] {
        const out = new HTMLCollection<Node>();
        collectByClass(this, String(className), out);
        return out as unknown as Element[];
    }

    get innerHTML(): string {
        return this.cachedInnerHTML != null ? this.cachedInnerHTML : serializeChildren(this);
    }

    // Parse into real child nodes (so cloneNode/lastChild/querySelector and the link collector see injected
    // content — CMS rich-text and dangerouslySetInnerHTML blocks carry anchors), then keep the verbatim
    // string as a serialization fast-path. Any later child mutation nulls the cache via appendChild et al.
    set innerHTML(v: unknown) {
        this.childNodes = [];
        const html = v == null ? "" : String(v);
        const parse = parserRef.parseFragment;
        if (parse) for (const node of parse(html, this.localName)) this.appendChild(node);
        this.cachedInnerHTML = html;
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

    // Replaces this element with the parsed markup in its parent (custom elements self-unwrap via
    // `el.outerHTML = el.innerHTML`). A detached element has nowhere to go, so it's a no-op, matching the
    // getter-only shape bundles otherwise trip over.
    set outerHTML(v: unknown) {
        const parent = this.parentNode;
        if (!parent) return;
        const html = v == null ? "" : String(v);
        const parse = parserRef.parseFragment;
        const nodes = parse ? parse(html, (parent as any).localName) : [];
        for (const node of nodes) parent.insertBefore(node, this);
        parent.removeChild(this);
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

    getAnimations(): Animation[] {
        return [];
    }

    // The three adjacent-insertion methods, which a widget uses in place of innerHTML precisely because it
    // must not disturb the siblings already there. Called bare, so absence is a TypeError that costs the
    // whole script rather than one insertion. An unknown position is a no-op here, where a browser throws:
    // the render has nothing to gain from ending a script over a misspelt argument.
    insertAdjacentElement(position: string, element: Node): Node | null {
        this.insertAdjacent(position, [element]);
        return element;
    }

    insertAdjacentText(position: string, text: unknown): void {
        this.insertAdjacent(position, [new Text(text == null ? "" : String(text))]);
    }

    insertAdjacentHTML(position: string, html: unknown): void {
        const parse = parserRef.parseFragment;
        this.insertAdjacent(position, parse ? parse(html == null ? "" : String(html)) : []);
    }

    private insertAdjacent(position: string, nodes: Node[]): void {
        const where = String(position).toLowerCase();
        const parent = this.parentNode;

        if (where === "beforeend") {
            for (const node of nodes) this.appendChild(node);
        } else if (where === "afterbegin") {
            const first = this.childNodes[0] || null;
            for (const node of nodes) this.insertBefore(node, first);
        } else if (where === "beforebegin" && parent) {
            for (const node of nodes) parent.insertBefore(node, this);
        } else if (where === "afterend" && parent) {
            const next = this.nextSibling;
            for (const node of nodes) parent.insertBefore(node, next);
        }
    }
}
