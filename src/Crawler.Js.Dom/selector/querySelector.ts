import type { Node } from "../dom/Node";
import { NodeType } from "../types/NodeType";

export function querySelectorAll(root: Node, sel: string): Node[] {
    const el: Node = (root as any).documentElement || root;
    const out: Node[] = [];
    const s = String(sel).trim();
    const idM = s.match(/^#([\w-]+)$/);
    const attrM = s.match(/^(\w+)?\[([\w-]+)(?:[~|]?=["']?([^"'\]]*)["']?)?\]$/);

    walk(el);
    return out;

    function walk(n: Node): void {
        if (n.nodeType === NodeType.Element && matches(n)) out.push(n);
        for (const c of n.childNodes) walk(c);
    }

    function matches(n: Node): boolean {
        const e = n as any;
        if (idM) return e.getAttribute("id") === idM[1];
        if (attrM) {
            if (attrM[1] && e.localName !== attrM[1].toLowerCase()) return false;
            if (!e.hasAttribute(attrM[2])) return false;
            if (attrM[3] != null && attrM[3] !== "") return e.getAttribute(attrM[2]) === attrM[3];
            return true;
        }
        return e.localName === s.toLowerCase();
    }
}
