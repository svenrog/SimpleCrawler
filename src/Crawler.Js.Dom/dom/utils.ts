import type { Node } from "./Node";
import { NodeType } from "../types/NodeType";

// Browser DOM nodes report no own enumerable keys — all state lives on the prototype. Bundles that
// deep-walk a node (JSON.stringify, structural cloners) depend on that: with enumerable instance fields the
// parentNode/childNodes back-reference is a cycle they recurse through forever. Each node constructor hides
// the fields it just added; Object.keys reports only still-enumerable ones, so the inheritance chain seals
// every field exactly once.
export function hideOwnFields(node: object): void {
    const keys = Object.keys(node);
    for (let i = 0; i < keys.length; i++) {
        Object.defineProperty(node, keys[i], { enumerable: false });
    }
}

export function escapeAttr(v: unknown): string {
    return String(v).replace(/&/g, "&amp;").replace(/"/g, "&quot;");
}

export function escapeText(v: unknown): string {
    return String(v).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

export function collectByTag(node: Node, tag: string, out: Node[]): void {
    const kids = node.childNodes;
    for (let i = 0; i < kids.length; i++) {
        const c = kids[i];
        if (c.nodeType === NodeType.Element) {
            if ((c as any).localName === tag) out.push(c);
            collectByTag(c, tag, out);
        }
    }
}

export function textOf(node: Node): string {
    if (node.nodeType === NodeType.Text) return (node as any).data;
    let s = "";
    for (const c of node.childNodes) s += textOf(c);
    return s;
}

export function walkFind(node: Node | null, pred: (e: Node) => boolean): Node | null {
    if (!node) return null;
    if (node.nodeType === NodeType.Element && pred(node)) return node;
    for (const c of node.childNodes) {
        const r = walkFind(c, pred);
        if (r) return r;
    }
    return null;
}
