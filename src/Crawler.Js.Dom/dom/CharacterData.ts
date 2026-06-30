import { Node } from "./Node";
import { NodeType } from "../types/NodeType";

export abstract class CharacterData extends Node {
    data: string;

    protected constructor(type: NodeType, data: unknown) {
        super(type);
        this.data = data == null ? "" : String(data);
    }

    get nodeValue(): string {
        return this.data;
    }
    set nodeValue(v: unknown) {
        this.data = v == null ? "" : String(v);
    }
}
