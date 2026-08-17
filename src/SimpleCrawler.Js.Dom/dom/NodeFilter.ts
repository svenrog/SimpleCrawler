// The bit constants a caller passes as whatToShow, and the verdicts a filter returns. A page reaches these
// through the global (`NodeFilter.SHOW_ELEMENT`) before it ever touches a walker, so the object has to exist
// even where createTreeWalker's result goes unused.
export const NodeFilter = {
    FILTER_ACCEPT: 1,
    FILTER_REJECT: 2,
    FILTER_SKIP: 3,
    SHOW_ALL: 0xffffffff,
    SHOW_ELEMENT: 0x1,
    SHOW_ATTRIBUTE: 0x2,
    SHOW_TEXT: 0x4,
    SHOW_CDATA_SECTION: 0x8,
    SHOW_ENTITY_REFERENCE: 0x10,
    SHOW_ENTITY: 0x20,
    SHOW_PROCESSING_INSTRUCTION: 0x40,
    SHOW_COMMENT: 0x80,
    SHOW_DOCUMENT: 0x100,
    SHOW_DOCUMENT_TYPE: 0x200,
    SHOW_DOCUMENT_FRAGMENT: 0x400,
    SHOW_NOTATION: 0x800,
};

// whatToShow is a bitmask over (nodeType - 1); a filter may be the callback itself or an object carrying
// acceptNode, and a filter that throws must not escape into the caller's traversal.
export function accepts(node: any, whatToShow: number, filter: any): number {
    if (((1 << (node.nodeType - 1)) & whatToShow) === 0) return NodeFilter.FILTER_SKIP;
    if (!filter) return NodeFilter.FILTER_ACCEPT;

    const accept = typeof filter === "function" ? filter : filter.acceptNode;
    if (typeof accept !== "function") return NodeFilter.FILTER_ACCEPT;

    const verdict = accept.call(filter, node);
    return verdict === NodeFilter.FILTER_REJECT || verdict === NodeFilter.FILTER_SKIP
        ? verdict
        : NodeFilter.FILTER_ACCEPT;
}

// Document-order successor of `node`, bounded by `root`. `skipChildren` is the REJECT case: the subtree is
// abandoned rather than descended.
export function nextInOrder(node: any, root: any, skipChildren: boolean): any {
    if (!skipChildren && node.childNodes.length) return node.childNodes[0];

    let current = node;
    while (current && current !== root) {
        const sibling = current.nextSibling;
        if (sibling) return sibling;
        current = current.parentNode;
    }
    return null;
}

// Document-order predecessor of `node`, bounded by `root`: the deepest last descendant of the previous
// sibling, or the parent when there is none. The parent may be `root` itself — a NodeIterator may return it,
// a TreeWalker may not, so the caller decides rather than this.
export function previousInOrder(node: any, root: any): any {
    if (node === root) return null;

    let previous = node.previousSibling;
    if (!previous) return node.parentNode;

    while (previous.childNodes.length) previous = previous.childNodes[previous.childNodes.length - 1];
    return previous;
}
