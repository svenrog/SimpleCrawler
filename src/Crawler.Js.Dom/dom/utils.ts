import type { Node } from "./Node";
import { NodeType } from "../types/NodeType";

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
