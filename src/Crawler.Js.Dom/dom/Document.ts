import { Node } from "./Node";
import { NodeType } from "../types/NodeType";
import { Element } from "./Element";
import { Text } from "./Text";
import { Comment } from "./Comment";
import { DocumentFragment } from "./DocumentFragment";
import { collectByTag, walkFind } from "./utils";
import { querySelectorAll } from "../selector/querySelector";

export class Document extends Node {
    documentElement: Element | null = null;
    head: Element | null = null;
    body: Element | null = null;
    defaultView: any;
    styleSheets: any[] = [];

    constructor(defaultView?: any) {
        super(NodeType.Document);
        this.defaultView = defaultView || null;
    }

    createElement(tag: string): Element {
        return new Element(tag);
    }

    createElementNS(ns: string, tag: string): Element {
        return new Element(tag, ns);
    }

    createTextNode(data: unknown): Text {
        return new Text(data);
    }

    createComment(data: unknown): Comment {
        return new Comment(data);
    }

    createDocumentFragment(): DocumentFragment {
        return new DocumentFragment();
    }

    getElementById(id: string): Element | null {
        return walkFind(this.documentElement, (e) => (e as any).getAttribute("id") === id) as Element | null;
    }

    getElementsByTagName(tag: string): Element[] {
        const out: Node[] = [];
        if (this.documentElement) collectByTag(this.documentElement, String(tag).toLowerCase(), out);
        return out as unknown as Element[];
    }

    querySelector(sel: string): Element | null {
        const r = querySelectorAll(this, sel);
        return r.length ? (r[0] as Element) : null;
    }

    querySelectorAll(sel: string): Element[] {
        return querySelectorAll(this, sel) as unknown as Element[];
    }

    addEventListener(): void { }
    removeEventListener(): void { }

    createEvent(): any {
        return { initEvent() { } };
    }
}
