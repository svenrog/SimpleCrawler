import { Node } from "./Node";
import { CharacterData } from "./CharacterData";
import { NodeType } from "../types/NodeType";
import { hideOwnFields } from "./utils";

// Never produced by an HTML parse, and never serialized — it exists because the shadow-DOM polyfill patches
// the character-data types by name (`["Text","Comment","CDATASection","ProcessingInstruction"]
// .forEach(a => window[a].prototype…)`) and reads `.prototype` off each without checking it is there.
export class CDATASection extends CharacterData {
    constructor(data: unknown) {
        super(NodeType.CdataSection, data);
        hideOwnFields(this);
    }

    get nodeName(): string {
        return "#cdata-section";
    }

    protected _shallowClone(): Node {
        return new CDATASection(this.data);
    }
}
