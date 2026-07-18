import type { Node } from "../dom/Node";
import { Document } from "../dom/Document";
import { NodeType } from "../types/NodeType";
import { parseHTML, parseFragment } from "../html/parser";

// Bundles that build or fetch markup as a string and then query it construct one of these (a self-hosted Git
// forge parses a document to read its window.config, and never sets its version global when the constructor is
// missing). text/html
// nests under html/head/body exactly as the document parser does; xml/svg keeps the parsed root element as
// documentElement, since XML has no implied body. The HTML tokenizer backs both — case-folding and error
// recovery differ from a real XML parser, but no target reached here depends on that. Never throws: malformed
// input yields a near-empty document, the way a browser returns a <parsererror> document rather than raising.
export class DOMParser {
    parseFromString(input: unknown, type?: unknown): Document {
        const mime = String(type ?? "").toLowerCase();
        const doc = new Document();

        if (mime.indexOf("xml") >= 0 || mime.indexOf("svg") >= 0) {
            const root = parseFragment(input).find((n: Node) => n.nodeType === NodeType.Element) || null;
            if (root) {
                (root as any).parentNode = doc;
                doc.documentElement = root as any;
                doc.childNodes = [root];
            }
            return doc;
        }

        parseHTML(doc, input);
        return doc;
    }
}
