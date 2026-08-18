import { Node } from "./Node";
import { CharacterData } from "./CharacterData";
import { NodeType } from "../types/NodeType";
import { hideOwnFields } from "./utils";

// Same reason as [CDATASection]: the shadow-DOM polyfill patches the character-data types by name and
// dereferences each one's prototype unchecked. An HTML parse never produces one.
export class ProcessingInstruction extends CharacterData {
    readonly target: string;

    constructor(target: unknown, data: unknown) {
        super(NodeType.ProcessingInstruction, data);
        this.target = String(target ?? "");
        hideOwnFields(this);
    }

    get nodeName(): string {
        return this.target;
    }

    protected _shallowClone(): Node {
        return new ProcessingInstruction(this.target, this.data);
    }
}
