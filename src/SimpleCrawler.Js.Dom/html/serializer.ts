import type { Node } from "../dom/Node";
import { NodeType } from "../types/NodeType";
import { VOID_ELEMENTS, RAWTEXT_ELEMENTS } from "../constants";
import { escapeAttr, escapeText } from "../dom/utils";

export function serializeChildren(node: Node): string {
    const cached = (node as any).cachedInnerHTML;
    if (cached != null) return cached;
    // A raw-text element's children serialize literally — the parser never decoded entities inside one, so
    // escaping on the way out would corrupt what it holds rather than round-trip it. Page code round-trips
    // exactly this: a tag manager reads a staged <script>'s innerHTML and assigns it to a live script's
    // text, and every `&&` and `<` in the source comes back as an entity that no longer parses as JS.
    const raw = RAWTEXT_ELEMENTS[(node as any).localName];
    let s = "";
    for (const c of node.childNodes) s += raw && c.nodeType === NodeType.Text ? (c as any).data : serializeNode(c);
    return s;
}

export function serializeNode(node: Node): string {
    if (node.nodeType === NodeType.Text) return escapeText((node as any).data);
    if (node.nodeType === NodeType.Comment) return "<!--" + (node as any).data + "-->";
    if (node.nodeType === NodeType.DocumentFragment || node.nodeType === NodeType.Document) {
        return serializeChildren(node);
    }

    const el = node as any;
    const tag: string = el.localName;
    let s = "<" + tag;
    for (const k of el.getAttributeNames()) {
        s += " " + k + '="' + escapeAttr(el.getAttributeInternal(k)) + '"';
    }
    if (!el.hasAttribute("style")) {
        const css = el.style?.cssText;
        if (css) s += ' style="' + escapeAttr(css) + '"';
    }
    s += ">";
    if (VOID_ELEMENTS[tag]) return s;
    s += serializeChildren(el);
    return s + "</" + tag + ">";
}
