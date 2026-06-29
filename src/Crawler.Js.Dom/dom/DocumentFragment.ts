import { Node } from "./Node";
import { NodeType } from "../types/NodeType";
import { hideOwnFields } from "./utils";

export class DocumentFragment extends Node {
    constructor() {
        super(NodeType.DocumentFragment);
        hideOwnFields(this);
    }

    protected _shallowClone(): Node {
        return new DocumentFragment();
    }
}
