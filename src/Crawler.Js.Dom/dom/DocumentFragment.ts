import { Node } from "./Node";
import { NodeType } from "../types/NodeType";
import { hideOwnFields, collectByTag } from "./utils";
import { querySelectorAll } from "../selector/querySelector";

export class DocumentFragment extends Node {
    constructor() {
        super(NodeType.DocumentFragment);
        hideOwnFields(this);
    }

    get nodeName(): string {
        return "#document-fragment";
    }

    querySelector(sel: string): Node | null {
        const r = querySelectorAll(this, sel);
        return r.length ? r[0] : null;
    }

    querySelectorAll(sel: string): Node[] {
        return querySelectorAll(this, sel);
    }

    getElementsByTagName(tag: string): Node[] {
        const out: Node[] = [];
        collectByTag(this, String(tag).toLowerCase(), out);
        return out;
    }

    protected _shallowClone(): Node {
        return new DocumentFragment();
    }
}
