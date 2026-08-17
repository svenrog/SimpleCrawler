import type { Node } from "../dom/Node";
import { NodeType } from "../types/NodeType";
import { NodeList } from "../dom/NodeList";

export function querySelectorAll(root: Node, sel: string): Node[] {
    const el: Node = (root as any).documentElement || root;
    const out = new NodeList<Node>();
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

// Split on combinators/descendant whitespace to isolate the rightmost compound, but only at bracket/quote
// depth 0 — an attribute value can legitimately contain a space (e.g. [data-emotion^="css "]) and must not
// be treated as a descendant combinator, or the tail becomes garbage that matchesCompound then matches on.
function rightmostCompound(part: string): string {
    const s = part.trim();
    let start = 0;
    let depth = 0;
    let quote = "";
    for (let i = 0; i < s.length; i++) {
        const ch = s[i];
        if (quote) {
            if (ch === quote) quote = "";
            continue;
        }
        if (ch === '"' || ch === "'") quote = ch;
        else if (ch === "[") depth++;
        else if (ch === "]") { if (depth > 0) depth--; }
        else if (depth === 0 && (ch === ">" || ch === "+" || ch === "~" || /\s/.test(ch))) start = i + 1;
    }
    return s.slice(start);
}

function matchesCompound(el: any, compound: string): boolean {
    const re = /[#.]?[\w-]+|\[[^\]]*\]|\*/g;
    let m: RegExpExecArray | null;
    let matched = 0;
    while ((m = re.exec(compound))) {
        matched++;
        const tok = m[0];
        const c = tok[0];
        if (tok === "*") continue;
        if (c === "#") {
            if (el.getAttributeInternal("id") !== tok.slice(1)) return false;
        } else if (c === ".") {
            if (!hasClass(el, tok.slice(1))) return false;
        } else if (c === "[") {
            if (!matchesAttr(el, tok)) return false;
        } else if (el.localName !== tok.toLowerCase()) {
            return false;
        }
    }
    return matched > 0;
}

function hasClass(el: any, name: string): boolean {
    const cls = el.getAttributeInternal("class");
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
    const actual = el.getAttributeInternal(name) ?? "";
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
