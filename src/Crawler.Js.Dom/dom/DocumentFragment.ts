import { Node } from "./Node";
import { NodeType } from "../types/NodeType";

export class DocumentFragment extends Node {
    constructor() {
        super(NodeType.DocumentFragment);
    }
}
