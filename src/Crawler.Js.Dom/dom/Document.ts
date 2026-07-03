import { Node } from "./Node";
import { NodeType } from "../types/NodeType";
import { Element } from "./Element";
import { HTMLElement } from "./HTMLElement";
import { Text } from "./Text";
import { Comment } from "./Comment";
import { DocumentType } from "./DocumentType";
import { DocumentFragment } from "./DocumentFragment";
import { Range } from "./Range";
import { HTMLTemplateElement } from "./HTMLTemplateElement";
import { reflectedElementFactories } from "./reflectedElements";
import { customElements } from "./customElements";
import { collectByTag, walkFind, hideOwnFields, collectByClass } from "./utils";
import { querySelectorAll } from "../selector/querySelector";

export class Document extends Node {
    documentElement: Element | null = null;
    head: Element | null = null;
    body: Element | null = null;
    defaultView: any;
    styleSheets: any[] = [];
    // The <script> currently executing, set by the host around each classic script. Next's webpack
    // auto-public-path asserts it `instanceof HTMLScriptElement` and reads its src; outside execution it's null.
    currentScript: Element | null = null;
    private _cookies = new Map<string, string>();

    constructor(defaultView?: any) {
        super(NodeType.Document);
        this.defaultView = defaultView || null;
        hideOwnFields(this);
    }

    // Browsers expose document.location as an alias of window.location; scripts (analytics, Clerk's CDN
    // loader) read document.location.protocol/href, which threw on undefined when only window.location existed.
    // Clears per-page document state when the engine's realm is reused (Jint pool). The tree/head/body are
    // reassigned wholesale by the next parse (wireDocument), but cookies, adopted stylesheets and a dangling
    // currentScript would otherwise bleed into the next page, so they are dropped here.
    reset(): void {
        this.childNodes = [];
        this.documentElement = null;
        this.head = null;
        this.body = null;
        this.currentScript = null;
        this.styleSheets.length = 0;
        this._cookies.clear();
    }

    get location(): any {
        return this.defaultView ? this.defaultView.location : null;
    }

    // Bundles read document.referrer as a string (analytics, `referrer.split('/')[2] !== location.host`);
    // a single-pass render has no navigation history, so it's always the empty string.
    get referrer(): string {
        return "";
    }

    // A real document.cookie is always a string. Bundles probe it (document.cookie.includes(...)) and set it;
    // we keep a name→value store, ignoring attributes (path/expires/domain) and expiry since rendering is a
    // single synchronous pass.
    get cookie(): string {
        const out: string[] = [];
        for (const [k, v] of this._cookies) out.push(`${k}=${v}`);
        return out.join("; ");
    }

    set cookie(value: unknown) {
        const pair = String(value ?? "").split(";")[0];
        const eq = pair.indexOf("=");
        if (eq < 0) return;
        const name = pair.slice(0, eq).trim();
        if (name) this._cookies.set(name, pair.slice(eq + 1).trim());
    }

    createElement(tag: string): Element {
        const name = String(tag).toLowerCase();
        if (name === "template") return new HTMLTemplateElement();
        const factory = reflectedElementFactories[name];
        if (factory) return factory();
        const custom = customElements.tryCreate(name);
        return custom || new HTMLElement(name);
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

    createRange(): Range {
        return new Range();
    }

    getElementById(id: string): Element | null {
        return walkFind(this.documentElement, (e) => (e as any).getAttribute("id") === id) as Element | null;
    }

    getElementsByTagName(tag: string): Element[] {
        const out: Node[] = [];
        if (this.documentElement) collectByTag(this.documentElement, String(tag).toLowerCase(), out);
        return out as unknown as Element[];
    }

    getElementsByClassName(className: string): Element[] {
        const out: Node[] = [];
        if (this.documentElement) collectByClass(this.documentElement, String(className), out);
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

    createEvent(): any {
        return { initEvent() { } };
    }

    // jQuery's UMD factory feature-detects against `implementation.createHTMLDocument` during init; a missing
    // implementation threw before the global was assigned, so later bundles saw "jQuery is not defined".
    get implementation(): any {
        return {
            hasFeature: () => true,
            createDocumentType: (name: string, publicId?: string, systemId?: string) =>
                new DocumentType(name, publicId ?? "", systemId ?? ""),
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

    get nodeName(): string {
        return "#document";
    }

    get ownerDocument(): any {
        return null;
    }

    protected _shallowClone(): Node {
        return new Document(this.defaultView);
    }
}
