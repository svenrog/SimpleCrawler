import { Node } from "./Node";
import { CharacterData } from "./CharacterData";
import { NodeType } from "../types/NodeType";
import { hideOwnFields } from "./utils";

export class Text extends CharacterData {
    constructor(data: unknown) {
        super(NodeType.Text, data);
        hideOwnFields(this);
    }

    get nodeName(): string {
        return "#text";
    }

    get textContent(): string {
        return this.data;
    }
    set textContent(v: unknown) {
        this.data = v == null ? "" : String(v);
    }

    protected _shallowClone(): Node {
        return new Text(this.data);
    }
}
