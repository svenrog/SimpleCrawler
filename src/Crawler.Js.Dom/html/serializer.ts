import type { Node } from "../dom/Node";
import { NodeType } from "../types/NodeType";
import { VOID_ELEMENTS } from "../constants";
import { escapeAttr, escapeText } from "../dom/utils";

export function serializeChildren(node: Node): string {
    const cached = (node as any).cachedInnerHTML;
    if (cached != null) return cached;
    let s = "";
    for (const c of node.childNodes) s += serializeNode(c);
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
        s += " " + k + '="' + escapeAttr(el.getAttribute(k)) + '"';
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
