import { Node } from "./Node";
import { Element } from "./Element";
import { DocumentFragment } from "./DocumentFragment";
import { parseFragment } from "../html/parser";
import { serializeChildren } from "../html/serializer";
import { hideOwnFields } from "./utils";

// <template> holds its parsed markup in an inert .content fragment rather than as live children, so
// libraries that clone templates (Solid/Svelte compile to template().cloneNode(true)) read a real tree.
export class HTMLTemplateElement extends Element {
    readonly content: DocumentFragment;

    constructor() {
        super("template");
        this.content = new DocumentFragment();
        hideOwnFields(this);
    }

    get innerHTML(): string {
        return serializeChildren(this.content);
    }

    set innerHTML(v: unknown) {
        this.content.childNodes = [];
        for (const k of parseFragment(v)) this.content.appendChild(k);
    }

    protected _shallowClone(): Node {
        const clone = new HTMLTemplateElement();
        for (const c of this.content.childNodes) clone.content.appendChild(c.cloneNode(true));
        return clone;
    }
}
