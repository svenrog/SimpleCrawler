import type { Document } from "../dom/Document";
import { Element } from "../dom/Element";
import { Text } from "../dom/Text";
import { Comment } from "../dom/Comment";
import { createLocalElement, wireDocument, attachChild } from "./parser";

// Builds the document from a pre-parsed, parent-indexed node list (JSON from a native C# parser) so dom.js
// skips its own tokenizer. Mirrors parseHTML: nodes are created and attached while the tree is detached, and
// the document is wired last — connection-gated side effects (resource registration, connectedCallback) then
// behave exactly as for the string parser, and initial-page scripts are still discovered by collectScripts.
//
// Node shape: { k: 0=element|1=text|2=comment, t: tag, a: [[name,val],...], d: data, p: parentIndex }.
export function buildDocumentFromTree(doc: Document, json: string): void {
    const nodes: any[] = JSON.parse(json);
    const created: any[] = new Array(nodes.length);

    for (let i = 0; i < nodes.length; i++) {
        const n = nodes[i];
        let node: any;
        if (n.k === 0) {
            node = createLocalElement(n.t);
            const a = n.a;
            if (a) for (let j = 0; j < a.length; j++) node.setAttribute(a[j][0], a[j][1]);
        } else {
            const data = n.d == null ? "" : String(n.d);
            node = n.k === 1 ? new Text(data) : new Comment(data);
        }

        created[i] = node;
        const p = n.p;
        if (p >= 0) attachChild(created[p], node);
    }

    if (nodes.length === 0) return;

    const root = created[0];
    let head: Element | null = null;
    let body: Element | null = null;
    const kids = root.childNodes;
    for (let i = 0; i < kids.length; i++) {
        const k = kids[i];
        if (k.nodeType !== 1) continue;
        const tag = k.localName;
        if (head === null && tag === "head") head = k;
        else if (body === null && tag === "body") body = k;
    }

    wireDocument(doc, root, head, body);
}
