import { Node } from "./Node";
import { NodeType } from "../types/NodeType";
import { hideOwnFields } from "./utils";

export class Text extends Node {
    data: string;

    constructor(data: unknown) {
        super(NodeType.Text);
        this.data = data == null ? "" : String(data);
        hideOwnFields(this);
    }

    get nodeValue(): string {
        return this.data;
    }
    set nodeValue(v: unknown) {
        this.data = v == null ? "" : String(v);
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
