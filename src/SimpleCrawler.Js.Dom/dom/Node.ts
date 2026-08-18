import { NodeType } from "../types/NodeType";
import { documentRef } from "./documentRef";
import { hideOwnFields } from "./utils";
import { EventTarget } from "./eventTarget";

export abstract class Node extends EventTarget {
    readonly nodeType: NodeType;
    parentNode: Node | null = null;
    childNodes: Node[] = [];

    protected constructor(type: NodeType) {
        super();
        this.nodeType = type;
        hideOwnFields(this);
    }

    get ownerDocument(): any {
        return documentRef.current;
    }

    // Every node answers the document's base URL; Document overrides this with the computation. Bundles
    // resolve their own asset URLs against `node.baseURI` (a web component reading it off itself), where
    // undefined is a throw inside the component's constructor rather than a missed lookup.
    get baseURI(): string {
        const doc = this.ownerDocument;
        return doc ? doc.baseURI : "";
    }

    appendChild(child: Node): Node {
        return this.insertBefore(child, null);
    }

    insertBefore(child: Node, ref: Node | null): Node {
        if (child.nodeType === NodeType.DocumentFragment) {
            const kids = child.childNodes.slice();
            for (const k of kids) this.insertBefore(k, ref);
            return child;
        }
        if (child.parentNode) child.parentNode.removeChild(child);
        const at = ref ? this.childNodes.indexOf(ref) : -1;
        if (at < 0) this.childNodes.push(child);
        else this.childNodes.splice(at, 0, child);
        child.parentNode = this;
        return child;
    }

    removeChild(child: Node): Node {
        const at = this.childNodes.indexOf(child);
        if (at >= 0) {
            this.childNodes.splice(at, 1);
            child.parentNode = null;
        }
        return child;
    }

    replaceChild(n: Node, o: Node): Node {
        this.insertBefore(n, o);
        this.removeChild(o);
        return o;
    }

    remove(): void {
        if (this.parentNode) this.parentNode.removeChild(this);
    }

    hasChildNodes(): boolean {
        return this.childNodes.length > 0;
    }

    get firstChild(): Node | null {
        return this.childNodes[0] || null;
    }

    get lastChild(): Node | null {
        return this.childNodes[this.childNodes.length - 1] || null;
    }

    // Every node answers this, not only elements: it is Node.prototype's in a browser, and a text node's
    // parentElement is what a text-measuring or highlight library reads to find the box it sits in. An
    // accessibility overlay copies the descriptor off Node.prototype to wrap it, and finding none there
    // threw at defineProperty rather than skipping the wrap.
    get parentElement(): any {
        const p = this.parentNode;
        return p && p.nodeType === NodeType.Element ? p : null;
    }

    get nextSibling(): Node | null {
        if (!this.parentNode) return null;
        const s = this.parentNode.childNodes;
        const i = s.indexOf(this);
        return i >= 0 ? (s[i + 1] || null) : null;
    }

    get previousSibling(): Node | null {
        if (!this.parentNode) return null;
        const s = this.parentNode.childNodes;
        const i = s.indexOf(this);
        return i > 0 ? s[i - 1] : null;
    }

    // ChildNode / ParentNode insertion helpers. Svelte 5's compiled output threads its DOM through
    // anchor.before(node) and target.append(...nodes); a string argument becomes a Text node.
    before(...nodes: any[]): void {
        const parent = this.parentNode;
        if (!parent) return;
        for (const n of nodes) parent.insertBefore(asNode(n), this);
    }

    after(...nodes: any[]): void {
        const parent = this.parentNode;
        if (!parent) return;
        const ref = this.nextSibling;
        for (const n of nodes) parent.insertBefore(asNode(n), ref);
    }

    replaceWith(...nodes: any[]): void {
        const parent = this.parentNode;
        if (!parent) return;
        for (const n of nodes) parent.insertBefore(asNode(n), this);
        parent.removeChild(this);
    }

    append(...nodes: any[]): void {
        for (const n of nodes) this.appendChild(asNode(n));
    }

    prepend(...nodes: any[]): void {
        const ref = this.firstChild;
        for (const n of nodes) this.insertBefore(asNode(n), ref);
    }

    // Read before it is called — a consent banner swaps its markup in with
    // `host.replaceChildren.apply(host, Array.from(tmp.childNodes))` — so the gap is a throw inside that
    // banner's init, not a skipped update.
    replaceChildren(...nodes: any[]): void {
        for (const c of this.childNodes.slice()) this.removeChild(c);
        for (const n of nodes) this.appendChild(asNode(n));
    }

    cloneNode(deep?: boolean): Node {
        const clone = this._shallowClone();
        if (deep) for (const c of this.childNodes) clone.appendChild(c.cloneNode(true));
        return clone;
    }

    isEqualNode(other: Node | null): boolean {
        if (!other || other.nodeType !== this.nodeType) return false;

        const a = this as any;
        const b = other as any;
        if (this.nodeType === NodeType.Element) {
            if (a.nodeName !== b.nodeName || a.namespaceURI !== b.namespaceURI) return false;
            const names = a.getAttributeNames();
            if (names.length !== b.getAttributeNames().length) return false;
            for (const name of names) if (a.getAttributeInternal(name) !== b.getAttributeInternal(name)) return false;
        } else if (this.nodeType === NodeType.Text || this.nodeType === NodeType.Comment) {
            if (a.nodeValue !== b.nodeValue) return false;
        }

        if (this.childNodes.length !== other.childNodes.length) return false;
        for (let i = 0; i < this.childNodes.length; i++)
            if (!this.childNodes[i].isEqualNode(other.childNodes[i])) return false;

        return true;
    }

    isSameNode(other: Node | null): boolean {
        return other === this;
    }

    // React's getHoistableRoot falls back to `container.getRootNode?.() ?? container.ownerDocument` when the
    // container is the document itself (whose ownerDocument is null) — without this, that lookup throws.
    getRootNode(): Node {
        let n: Node = this;
        while (n.parentNode) n = n.parentNode;
        return n;
    }

    static readonly DOCUMENT_POSITION_DISCONNECTED = 1;
    static readonly DOCUMENT_POSITION_PRECEDING = 2;
    static readonly DOCUMENT_POSITION_FOLLOWING = 4;
    static readonly DOCUMENT_POSITION_CONTAINS = 8;
    static readonly DOCUMENT_POSITION_CONTAINED_BY = 16;
    static readonly DOCUMENT_POSITION_IMPLEMENTATION_SPECIFIC = 32;

    // Focus/tab-order libraries sort nodes with an `a.compareDocumentPosition(b)` comparator; without it the
    // sort throws inside a useMemo and the render subtree fails. Returns the bitmask describing `other`'s
    // position relative to `this`.
    compareDocumentPosition(other: Node): number {
        if (other === this) return 0;

        const thisChain: Node[] = [];
        for (let n: Node | null = this; n; n = n.parentNode) thisChain.push(n);
        const otherChain: Node[] = [];
        for (let n: Node | null = other; n; n = n.parentNode) otherChain.push(n);

        if (thisChain[thisChain.length - 1] !== otherChain[otherChain.length - 1])
            return Node.DOCUMENT_POSITION_DISCONNECTED | Node.DOCUMENT_POSITION_IMPLEMENTATION_SPECIFIC |
                Node.DOCUMENT_POSITION_FOLLOWING;

        if (otherChain.indexOf(this) >= 0)
            return Node.DOCUMENT_POSITION_CONTAINED_BY | Node.DOCUMENT_POSITION_FOLLOWING;
        if (thisChain.indexOf(other) >= 0)
            return Node.DOCUMENT_POSITION_CONTAINS | Node.DOCUMENT_POSITION_PRECEDING;

        thisChain.reverse();
        otherChain.reverse();
        let i = 0;
        while (thisChain[i] === otherChain[i]) i++;
        const kids = thisChain[i - 1].childNodes;
        return kids.indexOf(thisChain[i]) < kids.indexOf(otherChain[i])
            ? Node.DOCUMENT_POSITION_FOLLOWING
            : Node.DOCUMENT_POSITION_PRECEDING;
    }

    protected abstract _shallowClone(): Node;
}

function asNode(value: any): Node {
    return value instanceof Node ? value : documentRef.current.createTextNode(value);
}
