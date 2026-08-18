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
import { collectByTag, walkFind, hideOwnFields, collectByClass, collectByPredicate } from "./utils";
import { querySelectorAll } from "../selector/querySelector";
import { TreeWalker } from "./TreeWalker";
import { NodeIterator } from "./NodeIterator";
import { parserRef } from "../html/parserRef";
import { resolveUrl } from "../url/resolve";
import { HTMLCollection } from "./HTMLCollection";
import { createFontFaceSet } from "../browser/fonts";
import { viewportWidth, viewportHeight } from "../browser/viewport";

function withinViewport(x: unknown, y: unknown): boolean {
    const px = Number(x);
    const py = Number(y);
    return px >= 0 && py >= 0 && px <= viewportWidth() && py <= viewportHeight();
}

export class Document extends Node {
    documentElement: Element | null = null;
    head: Element | null = null;
    body: Element | null = null;
    defaultView: any;
    styleSheets: any[] = [];
    // The <script> currently executing, set by the host around each classic script. Next's webpack
    // auto-public-path asserts it `instanceof HTMLScriptElement` and reads its src; outside execution it's null.
    currentScript: Element | null = null;
    // Real navigation transitions loading→interactive→complete over time; this render parses the whole
    // document synchronously before any script runs, so by the time script code can observe it there is
    // nothing left "loading" — frameworks that gate on readyState (Next's Flight stream close among them)
    // see "complete" immediately instead of stalling behind a state that never advances.
    readyState: string = "complete";
    visibilityState: string = "visible";
    hidden: boolean = false;
    private _cookies = new Map<string, string>();
    private _fonts: any = null;

    constructor(defaultView?: any) {
        super(NodeType.Document);
        this.defaultView = defaultView || null;
        hideOwnFields(this);
    }

    // Browsers expose document.location as an alias of window.location; scripts (analytics, Clerk's CDN
    // loader) read document.location.protocol/href, which threw on undefined when only window.location existed.
    get location(): any {
        return this.defaultView ? this.defaultView.location : null;
    }

    // The page's own address, read as a string by consent/analytics code that never touches location
    // (`document.URL.indexOf(...)`, `new URL(document.documentURI)`). Both alias location.href here: this
    // render performs no navigation, so there is no history entry for them to diverge over.
    get URL(): string {
        const loc = this.location;
        return loc && loc.href ? String(loc.href) : "";
    }

    get documentURI(): string {
        return this.URL;
    }

    // The base against which the document's relative URLs resolve: the first <base href>, resolved against
    // the page URL, else the page URL itself. Node.baseURI delegates here for every node in the tree.
    get baseURI(): string {
        const base = this.querySelector("base");
        const href = base ? base.getAttributeInternal("href") : null;
        return href ? resolveUrl(href, this.URL) : this.URL;
    }

    // Bundles read document.referrer as a string (analytics, `referrer.split('/')[2] !== location.host`);
    // a single-pass render has no navigation history, so it's always the empty string.
    get referrer(): string {
        return "";
    }

    // document.domain mirrors the origin's hostname; scripts split/compare it and throw their own error when
    // it's undefined. The setter (legacy same-origin relaxation) is accepted and ignored — a single-pass render
    // never makes cross-origin calls that would consult it.
    get domain(): string {
        const loc = this.location;
        return loc && loc.hostname ? String(loc.hostname) : "";
    }

    set domain(_value: unknown) { }

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

    createTreeWalker(root: Node, whatToShow?: number, filter?: any): TreeWalker {
        return new TreeWalker(root, whatToShow, filter);
    }

    createNodeIterator(root: Node, whatToShow?: number, filter?: any): NodeIterator {
        return new NodeIterator(root, whatToShow, filter);
    }

    // Nothing here is written during parsing — the host parses the whole shell before any script runs — so a
    // write lands at the end of the body, which is where a trailing loader script's own write would have gone.
    // A written <script src> is a real appended resource: the drain loop fetches and runs it like any other.
    // Deliberately not the browser's post-load behaviour, which implicitly calls document.open() and wipes the
    // page: a bundle that writes after load would take the whole render's content with it.
    write(...parts: unknown[]): void {
        const target = this.body || this.documentElement;
        const parse = parserRef.parseFragment;
        if (!target || !parse) return;

        const html = parts.map((p) => (p == null ? "" : String(p))).join("");
        for (const node of parse(html)) target.appendChild(node);
    }

    writeln(...parts: unknown[]): void {
        this.write(parts.map((p) => (p == null ? "" : String(p))).join("") + "\n");
    }

    // A single-pass render has no parser to suspend and no stream to reopen; the pair exists so a loader that
    // brackets its write() with them doesn't throw on the way in or out.
    open(): Document {
        return this;
    }

    close(): void { }

    getElementById(id: string): Element | null {
        return walkFind(this.documentElement, (e) => (e as any).getAttributeInternal("id") === id) as Element | null;
    }

    // The root element is in scope for the document's own getElementsBy* — unlike an element's, which search
    // strictly below themselves. A browser answers document.getElementsByTagName("html") with the root, and
    // jQuery resolves a tag-only $("html") through exactly that call: an empty list there is undefined where
    // the caller expects an element, so `$("html").attr("lang").indexOf(...)` throws inside a CMS bundle's
    // init and costs every global it would have registered.
    getElementsByTagName(tag: string): Element[] {
        const name = String(tag).toLowerCase();
        const out = new HTMLCollection<Node>();
        if (this.documentElement) {
            if (name === "*" || this.documentElement.localName === name) out.push(this.documentElement);
            collectByTag(this.documentElement, name, out);
        }
        return out as unknown as Element[];
    }

    getElementsByClassName(className: string): Element[] {
        const out = new HTMLCollection<Node>();
        if (this.documentElement) {
            if ((this.documentElement as any).classList.contains(String(className))) out.push(this.documentElement);
            collectByClass(this.documentElement, String(className), out);
        }
        return out as unknown as Element[];
    }

    getElementsByName(name: string): Element[] {
        const matches = (e: any): boolean => e.getAttributeInternal("name") === name;
        const out = new HTMLCollection<Node>();
        if (this.documentElement) {
            if (matches(this.documentElement)) out.push(this.documentElement);
            collectByPredicate(this.documentElement, matches, out);
        }
        return out as unknown as Element[];
    }

    get scripts(): Element[] {
        return this.getElementsByTagName("script");
    }

    // The foreground answers: the tab is visible, it has focus, and nothing is focused past the body. Bot
    // management and session recorders read these during init and dereference what they get, so a missing one
    // throws instead of taking the backgrounded branch it was written for.
    hasFocus(): boolean {
        return true;
    }

    get activeElement(): Element | null {
        return this.body || this.documentElement;
    }

    // No layout, so nothing truly occupies a point. A recorder hit-testing its own cursor trail gets the
    // element a browser would always have under one, and null outside the viewport — the answer it guards
    // for already, since a browser returns null there too.
    elementFromPoint(x: unknown, y: unknown): Element | null {
        return withinViewport(x, y) ? (this.body || this.documentElement) : null;
    }

    elementsFromPoint(x: unknown, y: unknown): Element[] {
        if (!withinViewport(x, y)) return [];
        return [this.body, this.documentElement].filter((e) => e !== null) as Element[];
    }

    get fonts(): any {
        return this._fonts || (this._fonts = createFontFaceSet());
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
            // The XML sibling of createHTMLDocument, reached the same way: an SVG or feed helper calls it
            // during init with no feature test, so its absence costs that helper's whole script. The document
            // it answers with is an ordinary one carrying the named root element — namespaces are not modelled.
            createDocument: (_ns: string | null, qualifiedName?: string, doctype?: any) => {
                const d = new Document();
                if (doctype) d.appendChild(doctype);
                if (qualifiedName) {
                    const root = d.createElement(String(qualifiedName));
                    d.appendChild(root);
                    d.documentElement = root;
                }
                return d;
            },
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
