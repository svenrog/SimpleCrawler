import { Node } from "./Node";
import { NodeType } from "../types/NodeType";
import { hideOwnFields } from "./utils";

export class Comment extends Node {
    data: string;

    constructor(data: unknown) {
        super(NodeType.Comment);
        this.data = data == null ? "" : String(data);
        hideOwnFields(this);
    }

    protected _shallowClone(): Node {
        return new Comment(this.data);
    }
}
