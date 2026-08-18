import type { Node } from "../dom/Node";
import { NodeType } from "../types/NodeType";
import { NodeList } from "../dom/NodeList";

type SimpleKind = "type" | "universal" | "id" | "class" | "attr" | "pseudo";

interface Simple {
    kind: SimpleKind;
    name: string;
    // attr: the operator ("=", "^=", …) and the value it compares against; pseudo: a pre-parsed argument —
    // a selector list for :not/:is/:has, an { a, b } step for :nth-*.
    op?: string;
    value?: string;
    insensitive?: boolean;
    list?: Complex[];
    step?: { a: number; b: number };
}

interface Step {
    compound: Simple[];
    // How this step relates to the one before it. The first step carries "" unless the selector is relative
    // (":has(> .x)"), where the leading combinator stays on it for :has to read.
    combinator: "" | " " | ">" | "+" | "~";
}

type Complex = Step[];

// A selector string is parsed once and matched many times — event delegation runs the same `.matches('.x')`
// against every node it walks — so the parse is memoized. `null` means a selector this engine cannot
// represent; it matches nothing rather than throwing, because a page that guards nothing must not lose its
// whole bundle to a selector we do not model.
const _cache = new Map<string, Complex[] | null>();

export function querySelectorAll(root: Node, sel: string): Node[] {
    const out = new NodeList<Node>();
    const list = parseList(String(sel));
    if (!list) return out;

    // The root is the query's :scope but never a candidate itself — a browser's qSA only ever answers
    // descendants, so an element whose own class matches must not come back from its own query.
    const scope = root.nodeType === NodeType.Element ? (root as any) : null;
    const documentElement = (root as any).documentElement;
    if (documentElement) walk(documentElement);
    else for (const c of root.childNodes) walk(c);

    return out;

    function walk(n: Node): void {
        if (n.nodeType === NodeType.Element && matchesAny(n as any, list!, scope)) out.push(n);
        for (const c of n.childNodes) walk(c);
    }
}

// A single-element selector test backing Element.matches/closest (and the querySelectorAll walk). Combinators
// are matched right-to-left from the candidate, so an ancestor or sibling clause is a real constraint rather
// than the ignored prefix it used to be.
export function matchesSelector(el: any, selector: string): boolean {
    const list = parseList(String(selector));
    return !!list && matchesAny(el, list, null);
}

function matchesAny(el: any, list: Complex[], scope: any): boolean {
    for (const complex of list) {
        if (matchComplex(el, complex, complex.length - 1, scope)) return true;
    }
    return false;
}

function matchComplex(el: any, steps: Complex, index: number, scope: any): boolean {
    if (!matchCompound(el, steps[index].compound, scope)) return false;
    if (index === 0) return true;

    const combinator = steps[index].combinator;
    if (combinator === ">") {
        const p = el.parentNode;
        return !!p && p.nodeType === NodeType.Element && matchComplex(p, steps, index - 1, scope);
    }
    if (combinator === "+") {
        const s = previousElement(el);
        return !!s && matchComplex(s, steps, index - 1, scope);
    }
    if (combinator === "~") {
        for (let s = previousElement(el); s; s = previousElement(s)) {
            if (matchComplex(s, steps, index - 1, scope)) return true;
        }
        return false;
    }
    for (let p = el.parentNode; p && p.nodeType === NodeType.Element; p = p.parentNode) {
        if (matchComplex(p, steps, index - 1, scope)) return true;
    }
    return false;
}

function matchCompound(el: any, compound: Simple[], scope: any): boolean {
    for (const simple of compound) {
        if (!matchSimple(el, simple, scope)) return false;
    }
    return compound.length > 0;
}

function matchSimple(el: any, simple: Simple, scope: any): boolean {
    switch (simple.kind) {
        case "universal": return true;
        case "type": return el.localName === simple.name;
        case "id": return el.getAttributeInternal("id") === simple.name;
        case "class": return hasClass(el, simple.name);
        case "attr": return matchesAttr(el, simple);
        default: return matchesPseudo(el, simple, scope);
    }
}

function matchesPseudo(el: any, simple: Simple, scope: any): boolean {
    switch (simple.name) {
        case "not": return !matchesAny(el, simple.list!, scope);
        case "is":
        case "where":
        case "matches":
        case "-webkit-any":
        case "-moz-any": return matchesAny(el, simple.list!, scope);
        case "has": return matchesHas(el, simple.list!);
        case "scope": return scope ? el === scope : el === rootElement(el);
        case "root": return el === rootElement(el);
        case "empty": return isEmpty(el);
        case "first-child": return childIndex(el, false) === 1;
        case "last-child": return childIndex(el, true) === 1;
        case "only-child": return childIndex(el, false) === 1 && childIndex(el, true) === 1;
        case "first-of-type": return typeIndex(el, false) === 1;
        case "last-of-type": return typeIndex(el, true) === 1;
        case "only-of-type": return typeIndex(el, false) === 1 && typeIndex(el, true) === 1;
        case "nth-child": return matchesStep(childIndex(el, false), simple.step!) && matchesOf(el, simple, scope);
        case "nth-last-child": return matchesStep(childIndex(el, true), simple.step!) && matchesOf(el, simple, scope);
        case "nth-of-type": return matchesStep(typeIndex(el, false), simple.step!);
        case "nth-last-of-type": return matchesStep(typeIndex(el, true), simple.step!);
        case "checked": return el.hasAttribute("checked") || el.hasAttribute("selected") || el.checked === true;
        case "disabled": return el.hasAttribute("disabled");
        case "enabled": return !el.hasAttribute("disabled");
        case "required": return el.hasAttribute("required");
        case "optional": return !el.hasAttribute("required");
        case "read-only": return el.hasAttribute("readonly") || el.hasAttribute("disabled");
        case "read-write": return !el.hasAttribute("readonly") && !el.hasAttribute("disabled");
        case "any-link":
        case "link": return (el.localName === "a" || el.localName === "area") && el.hasAttribute("href");
        case "defined": return true;
        // Everything a single-pass render can never be in: no pointer, no focus, no navigation — and the
        // pseudo-elements, which match no element anywhere. A browser answers false to all of these too.
        default: return false;
    }
}

function matchesOf(el: any, simple: Simple, scope: any): boolean {
    return !simple.list || matchesAny(el, simple.list, scope);
}

function matchesHas(el: any, list: Complex[]): boolean {
    for (const complex of list) {
        const relation = complex[0].combinator;
        if (relation === "+" || relation === "~") {
            for (let s = nextElement(el); s; s = nextElement(s)) {
                if (matchComplex(s, complex, complex.length - 1, el)) return true;
                if (relation === "+") break;
            }
            continue;
        }
        if (descendantMatches(el, complex, el)) return true;
    }
    return false;
}

function descendantMatches(el: any, complex: Complex, scope: any): boolean {
    for (const c of el.childNodes) {
        if (c.nodeType !== NodeType.Element) continue;
        if (matchComplex(c, complex, complex.length - 1, scope)) return true;
        if (descendantMatches(c, complex, scope)) return true;
    }
    return false;
}

function rootElement(el: any): any {
    let cur = el;
    while (cur.parentNode && cur.parentNode.nodeType === NodeType.Element) cur = cur.parentNode;
    return cur;
}

function isEmpty(el: any): boolean {
    for (const c of el.childNodes) {
        if (c.nodeType === NodeType.Element) return false;
        if (c.nodeType === NodeType.Text && String(c.data || "").length > 0) return false;
    }
    return true;
}

function previousElement(el: any): any {
    let n = el.previousSibling;
    while (n && n.nodeType !== NodeType.Element) n = n.previousSibling;
    return n || null;
}

function nextElement(el: any): any {
    let n = el.nextSibling;
    while (n && n.nodeType !== NodeType.Element) n = n.nextSibling;
    return n || null;
}

// 1-based position among element siblings, counted from the end when `fromEnd`; 0 for an element with no
// parent, which no :nth-* step matches.
function childIndex(el: any, fromEnd: boolean): number {
    const p = el.parentNode;
    if (!p) return 0;
    let index = 0;
    let found = 0;
    for (const c of p.childNodes) {
        if (c.nodeType !== NodeType.Element) continue;
        index++;
        if (c === el) found = index;
    }
    return found === 0 ? 0 : fromEnd ? index - found + 1 : found;
}

function typeIndex(el: any, fromEnd: boolean): number {
    const p = el.parentNode;
    if (!p) return 0;
    let index = 0;
    let found = 0;
    for (const c of p.childNodes) {
        if (c.nodeType !== NodeType.Element || c.localName !== el.localName) continue;
        index++;
        if (c === el) found = index;
    }
    return found === 0 ? 0 : fromEnd ? index - found + 1 : found;
}

function matchesStep(position: number, step: { a: number; b: number }): boolean {
    if (position === 0) return false;
    if (step.a === 0) return position === step.b;
    const n = (position - step.b) / step.a;
    return n >= 0 && Number.isInteger(n);
}

function hasClass(el: any, name: string): boolean {
    const cls = el.getAttributeInternal("class");
    if (!cls) return false;
    return cls.split(/\s+/).indexOf(name) >= 0;
}

function matchesAttr(el: any, simple: Simple): boolean {
    if (!el.hasAttribute(simple.name)) return false;
    if (!simple.op) return true;

    const expected = simple.insensitive ? simple.value!.toLowerCase() : simple.value!;
    const raw = el.getAttributeInternal(simple.name) ?? "";
    const actual = simple.insensitive ? raw.toLowerCase() : raw;
    switch (simple.op) {
        case "=": return actual === expected;
        case "~=": return expected !== "" && actual.split(/\s+/).indexOf(expected) >= 0;
        case "|=": return actual === expected || actual.startsWith(expected + "-");
        case "^=": return expected !== "" && actual.startsWith(expected);
        case "$=": return expected !== "" && actual.endsWith(expected);
        case "*=": return expected !== "" && actual.indexOf(expected) >= 0;
        default: return true;
    }
}

function parseList(selector: string): Complex[] | null {
    const cached = _cache.get(selector);
    if (cached !== undefined) return cached;

    let parsed: Complex[] | null;
    try {
        parsed = parseSelectorList(selector);
    } catch {
        parsed = null;
    }
    // Unbounded growth would hold every one-off selector a bundle ever builds. The cap sits far above what a
    // page's own queries reach; past it the parse simply runs each time.
    if (_cache.size < 4096) _cache.set(selector, parsed);
    return parsed;
}

function parseSelectorList(selector: string): Complex[] | null {
    const out: Complex[] = [];
    for (const part of splitTopLevel(selector, ",")) {
        const complex = parseComplex(part);
        if (!complex) return null;
        out.push(complex);
    }
    return out.length ? out : null;
}

// Splits on `sep` at bracket/paren/quote depth 0: an attribute value or a :not() argument can carry the
// separator and must not be cut at it.
function splitTopLevel(input: string, sep: string): string[] {
    const out: string[] = [];
    let depth = 0;
    let quote = "";
    let start = 0;
    for (let i = 0; i < input.length; i++) {
        const ch = input[i];
        if (ch === "\\") { i++; continue; }
        if (quote) {
            if (ch === quote) quote = "";
            continue;
        }
        if (ch === '"' || ch === "'") quote = ch;
        else if (ch === "[" || ch === "(") depth++;
        else if (ch === "]" || ch === ")") { if (depth > 0) depth--; }
        else if (depth === 0 && ch === sep) { out.push(input.slice(start, i)); start = i + 1; }
    }
    out.push(input.slice(start));
    return out;
}

function parseComplex(part: string): Complex | null {
    const s = part.trim();
    if (!s) return null;

    const steps: Complex = [];
    let combinator: Step["combinator"] = "";
    let i = 0;
    while (i < s.length) {
        let spaced = false;
        while (i < s.length && /\s/.test(s[i])) { i++; spaced = true; }
        if (i >= s.length) break;

        const ch = s[i];
        if (ch === ">" || ch === "+" || ch === "~") {
            combinator = ch;
            i++;
            continue;
        }
        if (spaced && combinator === "" && steps.length) combinator = " ";

        const end = compoundEnd(s, i);
        const compound = parseCompound(s.slice(i, end));
        if (!compound) return null;
        steps.push({ compound, combinator: steps.length === 0 ? combinator : combinator || " " });
        combinator = "";
        i = end;
    }
    return steps.length ? steps : null;
}

function compoundEnd(s: string, from: number): number {
    let depth = 0;
    let quote = "";
    for (let i = from; i < s.length; i++) {
        const ch = s[i];
        if (ch === "\\") { i++; continue; }
        if (quote) {
            if (ch === quote) quote = "";
            continue;
        }
        if (ch === '"' || ch === "'") quote = ch;
        else if (ch === "[" || ch === "(") depth++;
        else if (ch === "]" || ch === ")") { if (depth > 0) depth--; }
        else if (depth === 0 && (/\s/.test(ch) || ch === ">" || ch === "+" || ch === "~")) return i;
    }
    return s.length;
}

function parseCompound(text: string): Simple[] | null {
    const out: Simple[] = [];
    let i = 0;
    while (i < text.length) {
        const ch = text[i];
        if (ch === "*") { out.push({ kind: "universal", name: "*" }); i++; continue; }
        if (ch === "#" || ch === ".") {
            const start = ++i;
            i = identEnd(text, i);
            if (i === start) return null;
            out.push({ kind: ch === "#" ? "id" : "class", name: unescapeIdent(text.slice(start, i)) });
            continue;
        }
        if (ch === "[") {
            const close = closingIndex(text, i, "[", "]");
            if (close < 0) return null;
            const attr = parseAttr(text.slice(i + 1, close));
            if (!attr) return null;
            out.push(attr);
            i = close + 1;
            continue;
        }
        if (ch === ":") {
            let start = i + 1;
            const doubled = text[start] === ":";
            if (doubled) start++;
            let end = identEnd(text, start);
            if (end === start) return null;
            const name = text.slice(start, end).toLowerCase();
            let arg = "";
            if (text[end] === "(") {
                const close = closingIndex(text, end, "(", ")");
                if (close < 0) return null;
                arg = text.slice(end + 1, close);
                end = close + 1;
            }
            i = end;
            const pseudo = parsePseudo(doubled ? "__never" : name, arg);
            if (!pseudo) return null;
            out.push(pseudo);
            continue;
        }

        const start = i;
        i = identEnd(text, i);
        if (i === start) return null;
        out.push({ kind: "type", name: unescapeIdent(text.slice(start, i)).toLowerCase() });
    }
    return out.length ? out : null;
}

function parsePseudo(name: string, arg: string): Simple | null {
    if (name === "not" || name === "is" || name === "where" || name === "matches" || name === "has"
        || name === "-webkit-any" || name === "-moz-any") {
        const list = parseSelectorList(arg);
        if (!list) return null;
        return { kind: "pseudo", name, list };
    }
    if (name === "nth-child" || name === "nth-last-child" || name === "nth-of-type" || name === "nth-last-of-type") {
        const parts = splitTopLevel(arg, " ").map((p) => p.trim()).filter((p) => p.length > 0);
        const step = parseStep(parts[0] || "");
        if (!step) return null;
        // ":nth-child(2 of .x)" narrows what the position counts. Applied as an extra match on the candidate
        // rather than a re-count, which differs only where non-matching siblings are interleaved.
        const of = parts.length >= 3 && parts[1].toLowerCase() === "of"
            ? parseSelectorList(parts.slice(2).join(" "))
            : null;
        return of ? { kind: "pseudo", name, step, list: of } : { kind: "pseudo", name, step };
    }
    return { kind: "pseudo", name, value: arg };
}

function parseStep(text: string): { a: number; b: number } | null {
    const s = text.trim().toLowerCase().replace(/\s+/g, "");
    if (s === "odd") return { a: 2, b: 1 };
    if (s === "even") return { a: 2, b: 0 };
    const m = s.match(/^([+-]?\d*)n([+-]\d+)?$/);
    if (m) {
        const a = m[1] === "" || m[1] === "+" ? 1 : m[1] === "-" ? -1 : Number(m[1]);
        return { a, b: m[2] ? Number(m[2]) : 0 };
    }
    if (/^[+-]?\d+$/.test(s)) return { a: 0, b: Number(s) };
    return null;
}

function parseAttr(text: string): Simple | null {
    const m = text.match(/^\s*([^\s~^$*|=\]]+)\s*(?:([~^$*|]?=)\s*(?:"([^"]*)"|'([^']*)'|([^\s\]]*))\s*([iIsS])?\s*)?$/);
    if (!m) return null;
    return {
        kind: "attr",
        name: unescapeIdent(m[1]),
        op: m[2],
        value: m[3] ?? m[4] ?? m[5] ?? "",
        insensitive: !!m[6] && m[6].toLowerCase() === "i",
    };
}

function closingIndex(text: string, from: number, open: string, close: string): number {
    let depth = 0;
    let quote = "";
    for (let i = from; i < text.length; i++) {
        const ch = text[i];
        if (ch === "\\") { i++; continue; }
        if (quote) {
            if (ch === quote) quote = "";
            continue;
        }
        if (ch === '"' || ch === "'") quote = ch;
        else if (ch === open) depth++;
        else if (ch === close) { depth--; if (depth === 0) return i; }
    }
    return -1;
}

// An identifier runs to the next structural character. Escapes are part of it — a Tailwind class is written
// `.md\:flex`, and stopping at the colon would read the rest of it as a pseudo-class.
function identEnd(text: string, from: number): number {
    let i = from;
    while (i < text.length) {
        const ch = text[i];
        if (ch === "\\") { i += 2; continue; }
        if (ch === "#" || ch === "." || ch === "[" || ch === "]" || ch === ":" || ch === "(" || ch === ")"
            || ch === "*" || ch === "," || ch === ">" || ch === "+" || ch === "~" || /\s/.test(ch)) break;
        i++;
    }
    return Math.min(i, text.length);
}

function unescapeIdent(text: string): string {
    return text.indexOf("\\") < 0 ? text : text.replace(/\\(.)/g, "$1");
}
