import type { Node } from "../dom/Node";
import { serializeNode } from "../html/serializer";

// The inverse of DOMParser, and reached by the same bundles: constructed bare during init (a self-hosted Git
// forge round-trips markup through parse-then-serialize), so a missing constructor is a ReferenceError that
// aborts the whole render — every global lost, not one. Delegates to the same serializer that backs
// Element.outerHTML, so the HTML and XML spellings agree — no target reached here needs the stricter XML
// output rules (namespace prefixing, empty elements as `<x/>`). Never throws: a non-node input serializes to "".
export class XMLSerializer {
    serializeToString(node: unknown): string {
        if (node == null || typeof (node as Node).nodeType !== "number") return "";
        return serializeNode(node as Node);
    }
}
