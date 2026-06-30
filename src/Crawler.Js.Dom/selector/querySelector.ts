import type { Node } from "../dom/Node";
import { NodeType } from "../types/NodeType";

export function querySelectorAll(root: Node, sel: string): Node[] {
    const el: Node = (root as any).documentElement || root;
    const out: Node[] = [];
    const s = String(sel).trim();

    walk(el);
    return out;

    function walk(n: Node): void {
        if (n.nodeType === NodeType.Element && matchesSelector(n as any, s)) out.push(n);
        for (const c of n.childNodes) walk(c);
    }
}

// A single-element selector test backing Element.matches/closest (and the querySelectorAll walk).
// Comma lists are an OR; each part is matched as a compound of simple selectors (tag/#id/.class/[attr]/*).
// Combinators aren't modelled, so a part containing one is reduced to its rightmost compound — enough to
// stop event-delegation code (el.matches('.x')) from throwing without claiming full Selectors-4 support.
export function matchesSelector(el: any, selector: string): boolean {
    const s = String(selector).trim();
    if (!s) return false;

    for (const part of s.split(",")) {
        const compound = rightmostCompound(part);
        if (compound && matchesCompound(el, compound)) return true;
    }
    return false;
}

function rightmostCompound(part: string): string {
    const tokens = part.trim().split(/\s*[>+~]\s*|\s+/);
    return tokens[tokens.length - 1];
}

function matchesCompound(el: any, compound: string): boolean {
    const re = /[#.]?[\w-]+|\[[^\]]*\]|\*/g;
    let m: RegExpExecArray | null;
    while ((m = re.exec(compound))) {
        const tok = m[0];
        const c = tok[0];
        if (tok === "*") continue;
        if (c === "#") {
            if (el.getAttribute("id") !== tok.slice(1)) return false;
        } else if (c === ".") {
            if (!hasClass(el, tok.slice(1))) return false;
        } else if (c === "[") {
            if (!matchesAttr(el, tok)) return false;
        } else if (el.localName !== tok.toLowerCase()) {
            return false;
        }
    }
    return true;
}

function hasClass(el: any, name: string): boolean {
    const cls = el.getAttribute("class");
    if (!cls) return false;
    return cls.split(/\s+/).indexOf(name) >= 0;
}

function matchesAttr(el: any, token: string): boolean {
    const m = token.match(/^\[([\w-]+)(?:([~|^$*]?=)["']?([^"'\]]*)["']?)?\]$/);
    if (!m) return false;

    const name = m[1];
    if (!el.hasAttribute(name)) return false;

    const op = m[2];
    if (!op) return true;

    const expected = m[3] ?? "";
    const actual = el.getAttribute(name) ?? "";
    switch (op) {
        case "=": return actual === expected;
        case "~=": return actual.split(/\s+/).indexOf(expected) >= 0;
        case "|=": return actual === expected || actual.startsWith(expected + "-");
        case "^=": return expected !== "" && actual.startsWith(expected);
        case "$=": return expected !== "" && actual.endsWith(expected);
        case "*=": return expected !== "" && actual.indexOf(expected) >= 0;
        default: return true;
    }
}
