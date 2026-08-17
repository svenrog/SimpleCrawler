import { NodeFilter, accepts, nextInOrder, previousInOrder } from "./NodeFilter";

// The flat counterpart to TreeWalker: the same filtered document-order sequence with no sideways axes.
// Sanitizers and text-extraction code pick one or the other by habit, so shipping the walker without this
// leaves the same ReferenceError behind for half of them. A REJECT verdict is treated as SKIP here, as the
// spec requires — only a TreeWalker abandons the subtree.
export class NodeIterator {
    readonly root: any;
    readonly whatToShow: number;
    readonly filter: any;
    referenceNode: any;
    pointerBeforeReferenceNode: boolean;

    constructor(root: any, whatToShow?: number, filter?: any) {
        this.root = root;
        this.whatToShow = whatToShow === undefined ? NodeFilter.SHOW_ALL : whatToShow >>> 0;
        this.filter = filter || null;
        this.referenceNode = root;
        this.pointerBeforeReferenceNode = true;
    }

    nextNode(): any {
        let node = this.referenceNode;
        let before = this.pointerBeforeReferenceNode;
        while (true) {
            if (before) before = false;
            else {
                node = nextInOrder(node, this.root, false);
                if (!node) return null;
            }

            if (accepts(node, this.whatToShow, this.filter) === NodeFilter.FILTER_ACCEPT) {
                this.referenceNode = node;
                this.pointerBeforeReferenceNode = false;
                return node;
            }
        }
    }

    previousNode(): any {
        let node = this.referenceNode;
        let before = this.pointerBeforeReferenceNode;
        while (true) {
            if (!before) before = true;
            else {
                node = previousInOrder(node, this.root);
                if (!node) return null;
            }

            if (accepts(node, this.whatToShow, this.filter) === NodeFilter.FILTER_ACCEPT) {
                this.referenceNode = node;
                this.pointerBeforeReferenceNode = true;
                return node;
            }
        }
    }

    // Detaching a NodeIterator has been a no-op since DOM4; callers written against the old API still call it.
    detach(): void { }
}
