import { Node } from "./Node";
import { CharacterData } from "./CharacterData";
import { NodeType } from "../types/NodeType";
import { hideOwnFields } from "./utils";

export class Comment extends CharacterData {
    constructor(data: unknown) {
        super(NodeType.Comment, data);
        hideOwnFields(this);
    }

    get nodeName(): string {
        return "#comment";
    }

    protected _shallowClone(): Node {
        return new Comment(this.data);
    }
}
