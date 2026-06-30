import type { Document } from "../dom/Document";
import type { Node } from "../dom/Node";
import { Element } from "../dom/Element";
import { HTMLAnchorElement } from "../dom/HTMLAnchorElement";
import { HTMLScriptElement } from "../dom/HTMLScriptElement";
import { HTMLLinkElement } from "../dom/HTMLLinkElement";
import { Text } from "../dom/Text";
import { Comment } from "../dom/Comment";
import { NodeType } from "../types/NodeType";
import { VOID_ELEMENTS, RAWTEXT_ELEMENTS } from "../constants";
import { decodeEntities } from "./entities";
import { createTagScanners, findRawTextClose } from "./tokenizer";

export function parseHTML(doc: Document, input: unknown): Element {
    const src = input == null ? "" : String(input);
    const len = src.length;
    const sc = createTagScanners();

    const root = new Element("html");
    const head = new Element("head");
    const body = new Element("body");
    root.appendChild(head);
    root.appendChild(body);

    let open: Element[] = [body];

    const cur = () => open[open.length - 1];
    function appendText(parent: Element, text: string): void {
        const last = parent.childNodes[parent.childNodes.length - 1] as any;
        if (last && last.nodeType === NodeType.Text) last.data += text;
        else parent.appendChild(new Text(text));
    }

    let i = 0;
    while (i < len) {
        const ch = src.charAt(i);
        if (ch !== "<") {
            let textEnd = src.indexOf("<", i);
            if (textEnd < 0) textEnd = len;
            appendText(cur(), decodeEntities(src.slice(i, textEnd)));
            i = textEnd;
            continue;
        }

        if (src.slice(i, i + 4) === "<!--") {
            const cEnd = src.indexOf("-->", i + 4);
            cur().appendChild(new Comment(src.slice(i + 4, cEnd < 0 ? len : cEnd)));
            i = cEnd < 0 ? len : cEnd + 3;
            continue;
        }
        if (src.charAt(i + 1) === "!" || src.charAt(i + 1) === "?") {
            const declEnd = src.indexOf(">", i);
            const end = declEnd < 0 ? len : declEnd;
            // <!doctype …> is a declaration with no node; every other <!…>/<?…> is a bogus comment that
            // the HTML parser surfaces as a Comment. Solid/Svelte emit <!$>/<!/> markers and walk them.
            const bang = src.charAt(i + 1) === "!";
            const inner = src.slice(i + 2, end);
            if (!bang) cur().appendChild(new Comment("?" + inner));
            else if (!/^doctype/i.test(inner)) cur().appendChild(new Comment(inner));
            i = declEnd < 0 ? len : declEnd + 1;
            continue;
        }
        if (src.charAt(i + 1) === "/") {
            sc.tagName.lastIndex = i + 2;
            const tm = sc.tagName.exec(src);
            if (tm) {
                const closeName = tm[0].toLowerCase();
                for (let k = open.length - 1; k > 0; k--) {
                    if (open[k].localName === closeName) { open.length = k; break; }
                }
            }
            const slashEnd = src.indexOf(">", i);
            i = slashEnd < 0 ? len : slashEnd + 1;
            continue;
        }

        sc.tagName.lastIndex = i + 1;
        const sm = sc.tagName.exec(src);
        if (!sm) { appendText(cur(), "<"); i++; continue; }
        const tag = sm[0].toLowerCase();
        let j = sc.tagName.lastIndex;
        let attrs: Record<string, string> | null = null;
        let selfClosed = false;

        while (j < len) {
            sc.ws.lastIndex = j;
            if (sc.ws.exec(src)) j = sc.ws.lastIndex;
            if (j >= len) break;
            const atC = src.charAt(j);
            if (atC === ">") { j++; break; }
            if (atC === "/" && src.charAt(j + 1) === ">") { selfClosed = true; j += 2; break; }
            sc.attrName.lastIndex = j;
            const am = sc.attrName.exec(src);
            if (!am) { j++; continue; }
            const an = am[0].toLowerCase();
            j = sc.attrName.lastIndex;
            sc.ws.lastIndex = j;
            if (sc.ws.exec(src)) j = sc.ws.lastIndex;
            let val = "";
            if (src.charAt(j) === "=") {
                j++;
                sc.ws.lastIndex = j;
                if (sc.ws.exec(src)) j = sc.ws.lastIndex;
                const quote = src.charAt(j);
                if (quote === '"' || quote === "'") {
                    const qEnd = src.indexOf(quote, j + 1);
                    val = decodeEntities(qEnd < 0 ? src.slice(j + 1) : src.slice(j + 1, qEnd));
                    j = qEnd < 0 ? len : qEnd + 1;
                } else {
                    sc.bareVal.lastIndex = j;
                    const bm = sc.bareVal.exec(src);
                    val = decodeEntities(bm ? bm[0] : "");
                    j = sc.bareVal.lastIndex;
                }
            }
            (attrs ||= {})[an] = val;
        }

        if (tag === "html") {
            if (attrs) for (const ha in attrs) root.setAttribute(ha, attrs[ha]);
            i = j;
            continue;
        }
        if (tag === "head") {
            open = [head];
            if (attrs) for (const he in attrs) head.setAttribute(he, attrs[he]);
            i = j;
            continue;
        }
        if (tag === "body") {
            open = [body];
            if (attrs) for (const bo in attrs) body.setAttribute(bo, attrs[bo]);
            i = j;
            continue;
        }

        const el = tag === "a" ? new HTMLAnchorElement()
            : tag === "script" ? new HTMLScriptElement()
                : tag === "link" ? new HTMLLinkElement()
                    : new Element(tag);
        if (attrs) for (const key in attrs) el.setAttribute(key, attrs[key]);

        if (RAWTEXT_ELEMENTS[tag]) {
            const rawFrom = j;
            const rawTo = findRawTextClose(src, tag, rawFrom);
            const raw = rawTo < 0 ? src.slice(rawFrom) : src.slice(rawFrom, rawTo);
            if (raw) el.appendChild(new Text(raw));
            const rawGt = rawTo < 0 ? len : src.indexOf(">", rawTo);
            i = rawGt < 0 ? len : rawGt + 1;
            cur().appendChild(el);
            continue;
        }

        cur().appendChild(el);
        if (!VOID_ELEMENTS[tag] && !selfClosed) open.push(el);
        i = j;
    }

    doc.documentElement = root;
    doc.head = head;
    doc.body = body;
    root.parentNode = doc;
    doc.childNodes = [root];
    return root;
}

// Parse a markup fragment (e.g. an innerHTML or <template> body) into detached nodes. The full-document
// parser nests everything under html/head/body, so the fragment's nodes are the resulting body's children.
export function parseFragment(html: unknown): Node[] {
    const scratch: any = {};
    parseHTML(scratch as Document, html);
    const kids = (scratch.body as Element).childNodes.slice();
    for (const k of kids) k.parentNode = null;
    return kids;
}
