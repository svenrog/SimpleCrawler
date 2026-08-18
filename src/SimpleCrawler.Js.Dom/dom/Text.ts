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

    // Splits at `offset`, keeping the head and returning the tail as the next sibling. Hydration walks a
    // server-rendered text run and splits it where the client tree expects a boundary; without this the
    // reconciler throws mid-commit and the subtree it was mounting is lost.
    splitText(offset: number): Text {
        const at = Math.max(0, Math.min(Number(offset) || 0, this.data.length));
        const tail = new Text(this.data.slice(at));
        this.data = this.data.slice(0, at);
        if (this.parentNode) this.parentNode.insertBefore(tail, this.nextSibling);
        return tail;
    }

    protected _shallowClone(): Node {
        return new Text(this.data);
    }
}
