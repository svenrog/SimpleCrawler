import { NodeFilter, accepts, nextInOrder, previousInOrder } from "./NodeFilter";

// A filtered view over the tree, positioned at currentNode. Editors, sanitizers and text-measuring code
// build one at init and step it; without it the constructing call throws and every global that script would
// have registered goes with it. Only the axes a single-pass render can answer are implemented — attribute
// and entity-reference node types don't exist in this DOM, so no whatToShow bit can select them.
export class TreeWalker {
    readonly root: any;
    readonly whatToShow: number;
    readonly filter: any;
    currentNode: any;

    constructor(root: any, whatToShow?: number, filter?: any) {
        this.root = root;
        this.whatToShow = whatToShow === undefined ? NodeFilter.SHOW_ALL : whatToShow >>> 0;
        this.filter = filter || null;
        this.currentNode = root;
    }

    private _accepts(node: any): number {
        return accepts(node, this.whatToShow, this.filter);
    }

    parentNode(): any {
        let node = this.currentNode;
        while (node && node !== this.root) {
            node = node.parentNode;
            if (node && this._accepts(node) === NodeFilter.FILTER_ACCEPT) {
                this.currentNode = node;
                return node;
            }
        }
        return null;
    }

    firstChild(): any {
        return this._child(true);
    }

    lastChild(): any {
        return this._child(false);
    }

    nextSibling(): any {
        return this._sibling(true);
    }

    previousSibling(): any {
        return this._sibling(false);
    }

    nextNode(): any {
        let node = this.currentNode;
        let verdict = NodeFilter.FILTER_ACCEPT;
        while (true) {
            node = nextInOrder(node, this.root, verdict === NodeFilter.FILTER_REJECT);
            if (!node) return null;

            verdict = this._accepts(node);
            if (verdict === NodeFilter.FILTER_ACCEPT) {
                this.currentNode = node;
                return node;
            }
        }
    }

    previousNode(): any {
        let node = this.currentNode;
        while (true) {
            node = previousInOrder(node, this.root);
            if (!node || node === this.root) return null;

            if (this._accepts(node) === NodeFilter.FILTER_ACCEPT) {
                this.currentNode = node;
                return node;
            }
        }
    }

    // A SKIP verdict looks through the node to its own children; a REJECT verdict abandons the subtree.
    private _child(forward: boolean): any {
        const kids = this.currentNode.childNodes;
        for (let i = 0; i < kids.length; i++) {
            const node = kids[forward ? i : kids.length - 1 - i];
            const verdict = this._accepts(node);
            if (verdict === NodeFilter.FILTER_ACCEPT) {
                this.currentNode = node;
                return node;
            }
            if (verdict === NodeFilter.FILTER_SKIP) {
                const saved = this.currentNode;
                this.currentNode = node;
                const descendant = this._child(forward);
                if (descendant) return descendant;
                this.currentNode = saved;
            }
        }
        return null;
    }

    private _sibling(forward: boolean): any {
        let node = this.currentNode;
        while (node && node !== this.root) {
            let sibling = forward ? node.nextSibling : node.previousSibling;
            while (sibling) {
                const verdict = this._accepts(sibling);
                if (verdict === NodeFilter.FILTER_ACCEPT) {
                    this.currentNode = sibling;
                    return sibling;
                }
                if (verdict === NodeFilter.FILTER_SKIP && sibling.childNodes.length) {
                    const saved = this.currentNode;
                    this.currentNode = sibling;
                    const descendant = this._child(forward);
                    if (descendant) return descendant;
                    this.currentNode = saved;
                }
                sibling = forward ? sibling.nextSibling : sibling.previousSibling;
            }
            node = node.parentNode;
        }
        return null;
    }
}
