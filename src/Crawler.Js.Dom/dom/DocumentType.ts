import { Node } from "./Node";
import { NodeType } from "../types/NodeType";
import { hideOwnFields } from "./utils";

export class DocumentType extends Node {
    readonly name: string;
    readonly publicId: string;
    readonly systemId: string;

    constructor(name: string, publicId = "", systemId = "") {
        super(NodeType.DocumentType);
        this.name = name;
        this.publicId = publicId;
        this.systemId = systemId;
        hideOwnFields(this);
    }

    get nodeName(): string {
        return this.name;
    }

    protected _shallowClone(): Node {
        return new DocumentType(this.name, this.publicId, this.systemId);
    }
}
