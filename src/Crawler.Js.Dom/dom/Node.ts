import { NodeType } from "../types/NodeType";
import { documentRef } from "./documentRef";

export abstract class Node {
    readonly nodeType: NodeType;
    parentNode: Node | null = null;
    childNodes: Node[] = [];

    protected constructor(type: NodeType) {
        this.nodeType = type;
    }

    get ownerDocument(): any {
        return documentRef.current;
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

    get firstChild(): Node | null {
        return this.childNodes[0] || null;
    }

    get lastChild(): Node | null {
        return this.childNodes[this.childNodes.length - 1] || null;
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

    cloneNode(deep?: boolean): Node {
        const clone = this._shallowClone();
        if (deep) for (const c of this.childNodes) clone.appendChild(c.cloneNode(true));
        return clone;
    }

    protected abstract _shallowClone(): Node;
}

function asNode(value: any): Node {
    return value instanceof Node ? value : documentRef.current.createTextNode(value);
}
