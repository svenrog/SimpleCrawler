import { Node } from "./Node";
import { NodeType } from "../types/NodeType";
import { Element } from "./Element";
import { Text } from "./Text";
import { Comment } from "./Comment";
import { DocumentFragment } from "./DocumentFragment";
import { HTMLTemplateElement } from "./HTMLTemplateElement";
import { HTMLAnchorElement } from "./HTMLAnchorElement";
import { HTMLScriptElement } from "./HTMLScriptElement";
import { HTMLLinkElement } from "./HTMLLinkElement";
import { customElements } from "./customElements";
import { collectByTag, walkFind, hideOwnFields } from "./utils";
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
        hideOwnFields(this);
    }

    createElement(tag: string): Element {
        const name = String(tag).toLowerCase();
        if (name === "template") return new HTMLTemplateElement();
        if (name === "a") return new HTMLAnchorElement();
        if (name === "script") return new HTMLScriptElement();
        if (name === "link") return new HTMLLinkElement();
        const custom = customElements.tryCreate(name);
        return custom || new Element(name);
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

    get scripts(): Element[] {
        return this.getElementsByTagName("script");
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

    // jQuery's UMD factory feature-detects against `implementation.createHTMLDocument` during init; a missing
    // implementation threw before the global was assigned, so later bundles saw "jQuery is not defined".
    get implementation(): any {
        return {
            hasFeature: () => true,
            createDocumentType: () => ({}),
            createHTMLDocument: (title?: string) => {
                const d = new Document();
                const html = d.createElement("html");
                const head = d.createElement("head");
                const body = d.createElement("body");
                html.appendChild(head);
                html.appendChild(body);
                d.appendChild(html);
                d.documentElement = html;
                d.head = head;
                d.body = body;
                if (title) {
                    const t = d.createElement("title");
                    t.textContent = title;
                    head.appendChild(t);
                }
                return d;
            },
        };
    }

    get ownerDocument(): any {
        return null;
    }

    protected _shallowClone(): Node {
        return new Document(this.defaultView);
    }
}
