import { DocumentFragment } from "./DocumentFragment";
import { parseFragment } from "../html/parser";

const _zeroRect = { top: 0, left: 0, right: 0, bottom: 0, width: 0, height: 0, x: 0, y: 0 };

// A headless Range with no layout: geometry is zero and boundary tracking is best-effort. The one method that
// must do real work is createContextualFragment — bundles parse HTML-string content through it (then walk the
// fragment for <script> tags), and a missing Range trips the page's error boundary mid-render.
export class Range {
    startContainer: any = null;
    endContainer: any = null;
    startOffset = 0;
    endOffset = 0;
    collapsed = true;
    commonAncestorContainer: any = null;

    setStart(node: any, offset: number): void { this.startContainer = node; this.startOffset = offset; }
    setEnd(node: any, offset: number): void { this.endContainer = node; this.endOffset = offset; }
    setStartBefore(node: any): void { this.startContainer = node; }
    setStartAfter(node: any): void { this.startContainer = node; }
    setEndBefore(node: any): void { this.endContainer = node; }
    setEndAfter(node: any): void { this.endContainer = node; }
    selectNode(node: any): void { this.startContainer = this.endContainer = this.commonAncestorContainer = node; }
    selectNodeContents(node: any): void { this.startContainer = this.endContainer = this.commonAncestorContainer = node; }
    collapse(): void { }
    cloneRange(): Range { return new Range(); }
    detach(): void { }
    insertNode(node: any): void {
        if (this.startContainer && typeof this.startContainer.appendChild === "function") this.startContainer.appendChild(node);
    }
    deleteContents(): void { }
    cloneContents(): DocumentFragment { return new DocumentFragment(); }
    extractContents(): DocumentFragment { return new DocumentFragment(); }
    surroundContents(): void { }
    getBoundingClientRect(): any { return _zeroRect; }
    getClientRects(): any[] { return []; }

    createContextualFragment(html: unknown): DocumentFragment {
        const fragment = new DocumentFragment();
        for (const node of parseFragment(html)) fragment.appendChild(node);
        return fragment;
    }
}
