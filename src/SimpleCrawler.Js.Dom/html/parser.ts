import type { Document } from "../dom/Document";
import type { Node } from "../dom/Node";
import { Element } from "../dom/Element";
import { HTMLElement } from "../dom/HTMLElement";
import { reflectedElementFactories } from "../dom/reflectedElements";
import { Text } from "../dom/Text";
import { Comment } from "../dom/Comment";
import { NodeType } from "../types/NodeType";
import { VOID_ELEMENTS, RAWTEXT_ELEMENTS } from "../constants";
import { decodeEntities } from "./entities";
import { isAlpha, skipSpace, matchTagName, scanAttrName, scanBareValue, findRawTextClose } from "./tokenizer";
import { parserRef } from "./parserRef";
import { markParserInserted } from "../dom/resourceLoader";

// The element-class selection the parser uses: known tag → reflected subclass, else a plain Element.
// Deliberately not document.createElement, which would consult customElements during the initial parse.
export function createLocalElement(tag: string): Element {
    const factory = reflectedElementFactories[tag];
    const el = factory ? factory() : new HTMLElement(tag);
    if (tag === "script") markParserInserted(el);
    return el;
}

// Append a freshly created node to the end of a parent during construction. The child has no prior parent,
// is never a DocumentFragment, and the tree is still detached from the document, so this skips appendChild's
// reparent/fragment handling and — crucially — the isConnected walk that would climb to the root on every
// insert (O(depth) per node). Connection-gated side effects run later, off wireDocument, exactly as before.
export function attachChild(parent: any, child: any): void {
    child.parentNode = parent;
    parent.childNodes.push(child);
}

// Attaches a finished root tree to the document, wiring documentElement/head/body.
export function wireDocument(doc: Document, root: Element, head: Element | null, body: Element | null): void {
    doc.documentElement = root;
    doc.head = head;
    doc.body = body;
    root.parentNode = doc;
    doc.childNodes = [root];
}

export function parseHTML(doc: Document, input: unknown): Element {
    const src = input == null ? "" : String(input);
    const len = src.length;

    const root = new HTMLElement("html");
    const head = new HTMLElement("head");
    const body = new HTMLElement("body");
    attachChild(root, head);
    attachChild(root, body);

    let open: Element[] = [body];

    function appendText(parent: Element, text: string): void {
        const last = parent.childNodes[parent.childNodes.length - 1] as any;
        if (last && last.nodeType === NodeType.Text) last.data += text;
        else attachChild(parent, new Text(text));
    }

    let i = 0;
    while (i < len) {
        if (src.charCodeAt(i) !== 60 /* < */) {
            let textEnd = src.indexOf("<", i);
            if (textEnd < 0) textEnd = len;
            appendText(open[open.length - 1], decodeEntities(src.slice(i, textEnd)));
            i = textEnd;
            continue;
        }

        const c1 = i + 1 < len ? src.charCodeAt(i + 1) : -1;

        // Opening tag — the common case, dispatched first so the branch predicts well.
        if (isAlpha(c1)) {
            const nameEnd = matchTagName(src, i + 1, len);
            const tag = src.slice(i + 1, nameEnd).toLowerCase();
            let j = nameEnd;

            // Select the target element up front and apply attributes straight to it — no intermediate
            // object, no second pass. html/head/body are structural (already under the root); everything
            // else is a fresh element that gets appended (and pushed, unless void/self-closed) below.
            const structural = tag === "html" || tag === "head" || tag === "body";
            let el: Element;
            if (tag === "html") el = root;
            else if (tag === "head") { open = [head]; el = head; }
            else if (tag === "body") { open = [body]; el = body; }
            else el = createLocalElement(tag);

            let selfClosed = false;
            while (j < len) {
                j = skipSpace(src, j, len);
                if (j >= len) break;
                const c = src.charCodeAt(j);
                if (c === 62 /* > */) { j++; break; }
                if (c === 47 /* / */ && src.charCodeAt(j + 1) === 62 /* > */) { selfClosed = true; j += 2; break; }

                const nameE = scanAttrName(src, j, len);
                if (nameE === j) { j++; continue; }
                const an = src.slice(j, nameE).toLowerCase();
                j = skipSpace(src, nameE, len);

                let val = "";
                if (src.charCodeAt(j) === 61 /* = */) {
                    j = skipSpace(src, j + 1, len);
                    const q = src.charCodeAt(j);
                    if (q === 34 /* " */ || q === 39 /* ' */) {
                        const qEnd = src.indexOf(src[j], j + 1);
                        val = decodeEntities(qEnd < 0 ? src.slice(j + 1) : src.slice(j + 1, qEnd));
                        j = qEnd < 0 ? len : qEnd + 1;
                    } else {
                        const vEnd = scanBareValue(src, j, len);
                        val = decodeEntities(src.slice(j, vEnd));
                        j = vEnd;
                    }
                }
                el.setAttributeInternal(an, val);
            }

            i = j;
            if (structural) continue;

            if (RAWTEXT_ELEMENTS[tag]) {
                const rawFrom = j;
                const rawTo = findRawTextClose(src, tag, rawFrom);
                const raw = rawTo < 0 ? src.slice(rawFrom) : src.slice(rawFrom, rawTo);
                if (raw) attachChild(el, new Text(raw));
                const rawGt = rawTo < 0 ? len : src.indexOf(">", rawTo);
                i = rawGt < 0 ? len : rawGt + 1;
                attachChild(open[open.length - 1], el);
                continue;
            }

            attachChild(open[open.length - 1], el);
            if (!VOID_ELEMENTS[tag] && !selfClosed) open.push(el);
            continue;
        }

        // Closing tag.
        if (c1 === 47 /* / */) {
            const nameEnd = matchTagName(src, i + 2, len);
            if (nameEnd >= 0) {
                const closeName = src.slice(i + 2, nameEnd).toLowerCase();
                for (let k = open.length - 1; k > 0; k--) {
                    if (open[k].localName === closeName) { open.length = k; break; }
                }
            }
            const gt = src.indexOf(">", i);
            i = gt < 0 ? len : gt + 1;
            continue;
        }

        // Comment.
        if (c1 === 33 /* ! */ && src.startsWith("<!--", i)) {
            const cEnd = src.indexOf("-->", i + 4);
            attachChild(open[open.length - 1], new Comment(src.slice(i + 4, cEnd < 0 ? len : cEnd)));
            i = cEnd < 0 ? len : cEnd + 3;
            continue;
        }

        // <!doctype …> is a declaration with no node; every other <!…>/<?…> is a bogus comment that the
        // HTML parser surfaces as a Comment. Solid/Svelte emit <!$>/<!/> markers and walk them.
        if (c1 === 33 /* ! */ || c1 === 63 /* ? */) {
            const declEnd = src.indexOf(">", i);
            const end = declEnd < 0 ? len : declEnd;
            const inner = src.slice(i + 2, end);
            if (c1 === 63) attachChild(open[open.length - 1], new Comment("?" + inner));
            else if (!/^doctype/i.test(inner)) attachChild(open[open.length - 1], new Comment(inner));
            i = declEnd < 0 ? len : declEnd + 1;
            continue;
        }

        // A '<' that starts nothing tag-like is literal text.
        appendText(open[open.length - 1], "<");
        i++;
    }

    wireDocument(doc, root, head, body);
    return root;
}

// Parse a markup fragment (e.g. an innerHTML or <template> body) into detached nodes. The full-document
// parser nests everything under html/head/body, so the fragment's nodes are the resulting body's children —
// except in <html> context, where the implied head and body are themselves what the fragment parses to. A
// sanitizer feature-tests itself by replacing a scratch document's body (`body.outerHTML = "<svg>…"`) and
// then looking the body up again; stripping the wrapper there leaves it looking at nothing and the whole
// library never registers.
export function parseFragment(html: unknown, context?: string): Node[] {
    const scratch: any = {};
    parseHTML(scratch as Document, html);
    const host = (context === "html" ? scratch.documentElement : scratch.body) as Element;
    const kids = host.childNodes.slice();
    for (const k of kids) k.parentNode = null;
    return kids;
}

parserRef.parseFragment = parseFragment;
