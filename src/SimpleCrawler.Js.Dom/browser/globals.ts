import { Document } from "../dom/Document";
import { Node } from "../dom/Node";
import { NodeList } from "../dom/NodeList";
import { Element } from "../dom/Element";
import { CharacterData } from "../dom/CharacterData";
import { Text } from "../dom/Text";
import { Comment } from "../dom/Comment";
import { DocumentType } from "../dom/DocumentType";
import { DocumentFragment } from "../dom/DocumentFragment";
import { HTMLElement } from "../dom/HTMLElement";
import { HTMLImageElement } from "../dom/HTMLImageElement";
import { HTMLTemplateElement } from "../dom/HTMLTemplateElement";
import { CSSTransition } from "../dom/CSSTransition";
import * as htmlInterfaces from "../dom/htmlInterfaces";
import { customElements } from "../dom/customElements";
import { navigator } from "./navigator";
import { createLocation } from "./location";
import { createHistory } from "./history";
import { installTimerGlobals } from "../scheduler/taskQueue";
import { URL } from "../url/URL";
import { URLSearchParams } from "../url/URLSearchParams";
import { Event } from "./Event";
import { CustomEvent } from "./CustomEvent";
import { PromiseRejectionEvent } from "./PromiseRejectionEvent";
import { DOMRect, DOMRectReadOnly } from "./DOMRect";
import { OffscreenCanvas } from "../dom/OffscreenCanvas";
import { TextEncoder } from "./TextEncoder";
import { TextDecoder } from "./TextDecoder";
import { crypto } from "./crypto";
import { AbortController } from "../network/types/AbortController";
import { AbortSignal } from "../network/types/AbortSignal";
import { XMLHttpRequestEventTarget } from "../network/XMLHttpRequestEventTarget";
import { XMLHttpRequestStub } from "../network/XMLHttpRequestStub";
import { MessageChannel, MessagePort } from "./MessageChannel";
import { createStorage } from "./Storage";
import { performance } from "./Performance";
import { installViewport } from "./viewport";
import { IntersectionObserver } from "./IntersectionObserver";
import { IntersectionObserverEntry } from "./IntersectionObserverEntry";
import { PerformanceObserver } from "./PerformanceObserver";
import { Worker } from "./Worker";
import { Blob } from "./Blob";
import { DOMException } from "./DOMException";
import { DOMParser } from "./DOMParser";
import { FileList } from "./FileList";
import { btoa, atob } from "./base64";
import { documentRef } from "../dom/documentRef";
import { createStyleDeclaration } from "../css/CSSStyleDeclaration";
import { installScrollApi } from "./scroll";
import { markPrototypeNative } from "./native";
import { EventListenerMap, EventTarget, addListener, removeListener, fireEvent } from "../dom/eventTarget";
import { reportSwallowed } from "../diagnostics";

export const doc = new Document(globalThis as any);
documentRef.current = doc;

export function installDOM(global: any): void {
    // window's own event listeners. Each realm renders exactly one page, so a fresh map per install is enough.
    const _windowListeners: EventListenerMap = {};

    global.document = doc;
    global.window = global;
    global.self = global;
    // A top-level browsing context with no child frames: window.frames/top/parent are all the window itself
    // and length is 0. Consent stubs probe for a sibling CMP with a bare `window.frames['__tcfapiLocator']`,
    // so leaving these out is a TypeError that kills the stub's whole script rather than a missed lookup.
    global.frames = global;
    global.top = global;
    global.parent = global;
    if (!("length" in global)) global.length = 0;
    global.navigator = navigator;
    global.location = createLocation();
    global.history = createHistory();
    // window is an EventTarget with the same real dispatch as document/Element; browser events like load/resize
    // are never synthesised in the single-pass render, so listeners only fire when a bundle dispatches explicitly.
    global.addEventListener = (t: string, cb: (...args: any[]) => void) => addListener(_windowListeners, t, cb);
    global.removeEventListener = (t: string, cb: (...args: any[]) => void) => removeListener(_windowListeners, t, cb);
    global.dispatchEvent = (event: any) => fireEvent(global, _windowListeners, event);
    // Handler props declared null so `'onX' in window` feature-detection passes and bundle assignments stick;
    // events never fire in the single-pass render, so the assigned handlers are only ever stored.
    for (const on of ["onresize", "onscroll", "onload", "onerror", "onunload", "onbeforeunload",
        "onpopstate", "onhashchange", "onpageshow", "onpagehide", "onmessage", "onoffline", "ononline",
        "onfocus", "onblur", "onorientationchange"]) {
        if (!(on in global)) global[on] = null;
    }
    // The global-scope counterpart to an uncaught throw: framework error boundaries call this explicitly to
    // surface a caught error the way an uncaught one would present. Forwards to window.onerror (if a bundle
    // set one) and to the diagnostics channel, so a call here is never silently lost.
    global.reportError = global.reportError || ((error: unknown) => {
        if (typeof global.onerror === "function") {
            try { global.onerror(error instanceof Error ? error.message : String(error), "", 0, 0, error); } catch { /* an onerror handler must not itself throw */ }
        }
        reportSwallowed("reportError", error);
    });
    // A real getComputedStyle returns a resolved CSSStyleDeclaration; the layout-less render can't resolve
    // values, so every property reads back "" (an empty string — never null/undefined). Bundles read computed
    // values both by name and as direct properties (Elementor's getCurrentDeviceMode does
    // `getComputedStyle(el, ':after').content.replace(...)`, which throws if `.content` is undefined), so a
    // fresh empty declaration — which returns "" for any key and via getPropertyValue — covers both paths.
    global.getComputedStyle = () => createStyleDeclaration();
    global.getSelection = () => ({
        rangeCount: 0,
        type: "None",
        isCollapsed: true,
        addRange() { },
        removeAllRanges() { },
        getRangeAt() { return null; },
        toString() { return ""; },
    });
    installViewport(global);
    global.MutationObserver = function () {
        this.observe = () => { };
        this.disconnect = () => { };
        this.takeRecords = () => [];
    };
    global.IntersectionObserver = IntersectionObserver;
    global.IntersectionObserverEntry = IntersectionObserverEntry;
    global.ResizeObserver = function () {
        this.observe = () => { };
        this.unobserve = () => { };
        this.disconnect = () => { };
    };
    global.PerformanceObserver = global.PerformanceObserver || PerformanceObserver;
    // Worker earns its place on how it is *reached*, which is the distinction that decides this whole class:
    // across sampled production bundles it is constructed bare (`new Worker(url)`, no feature test) and never
    // guarded, so its absence is not a branch a page chooses — it is a certain ReferenceError inside whatever
    // SDK constructs it. Nothing here runs the worker's script; the stub exists so that init survives.
    global.Worker = global.Worker || Worker;
    // declined: WebSocket, Notification. Both are plausible on the same story Worker is justified by — a chat
    // or push SDK constructing one during init — and neither was observed being constructed or feature-tested
    // on any sampled target, so there is no evidence to price. Worker's case is not that a story exists but
    // that the bare construction was counted in shipped code; absent that, this is how the prelude would
    // accumulate surface it cannot justify. Revisit when a target is shown losing a global to either.
    // A deep-clone good enough for the JSON-serialisable state bundles round-trip; non-JSON inputs
    // (functions, cycles) aren't supported, matching nothing real but never reached by our targets.
    global.structuredClone = global.structuredClone || ((value: any) => value == null ? value : JSON.parse(JSON.stringify(value)));
    global.Blob = Blob;
    global.DOMException = global.DOMException || DOMException;
    global.DOMParser = global.DOMParser || DOMParser;
    global.FileList = global.FileList || FileList;
    global.btoa = global.btoa || btoa;
    global.atob = global.atob || atob;
    // Blobs never leave the render, so an object URL only needs to be a unique, revocable token.
    (URL as any).createObjectURL = (URL as any).createObjectURL || (() => "blob:" + Math.random().toString(36).slice(2));
    (URL as any).revokeObjectURL = (URL as any).revokeObjectURL || (() => { });
    global.URL = URL;
    global.URLSearchParams = URLSearchParams;
    global.EventTarget = EventTarget;
    global.Node = Node;
    global.NodeList = NodeList;
    global.Element = Element;
    global.CharacterData = CharacterData;
    global.Document = Document;
    global.DocumentType = DocumentType;
    global.Text = Text;
    global.Comment = Comment;
    global.DocumentFragment = DocumentFragment;
    global.HTMLElement = HTMLElement;
    global.HTMLTemplateElement = HTMLTemplateElement;
    global.CSSTransition = CSSTransition;
    global.Image = HTMLImageElement;
    for (const name in htmlInterfaces) global[name] = (htmlInterfaces as any)[name];
    global.customElements = customElements;
    customElements.setDocument(doc);
    global.Event = Event;
    global.CustomEvent = CustomEvent;
    // A callable PromiseRejectionEvent keeps core-js from force-replacing the native Promise with a polyfill
    // whose finally/allSettled/withResolvers a bundle may have tree-shaken (native in real browsers).
    global.PromiseRejectionEvent = global.PromiseRejectionEvent || PromiseRejectionEvent;
    global.DOMRect = global.DOMRect || DOMRect;
    global.DOMRectReadOnly = global.DOMRectReadOnly || DOMRectReadOnly;
    global.OffscreenCanvas = global.OffscreenCanvas || OffscreenCanvas;
    global.TextEncoder = global.TextEncoder || TextEncoder;
    global.TextDecoder = global.TextDecoder || TextDecoder;
    global.crypto = global.crypto || crypto;
    global.AbortController = global.AbortController || AbortController;
    global.AbortSignal = global.AbortSignal || AbortSignal;
    // An inert XMLHttpRequest so unguarded prototype patching at SDK init doesn't throw; installNetwork swaps
    // in the functional one when the fetch shim is enabled (both extend the same-bundle event-target base).
    global.XMLHttpRequestEventTarget = global.XMLHttpRequestEventTarget || XMLHttpRequestEventTarget;
    global.XMLHttpRequest = global.XMLHttpRequest || XMLHttpRequestStub;
    global.MessageChannel = global.MessageChannel || MessageChannel;
    global.MessagePort = global.MessagePort || MessagePort;
    global.performance = global.performance || performance;
    global.localStorage = createStorage();
    global.sessionStorage = createStorage();
    installTimerGlobals(global);
    installScrollApi(global);
    for (const ctor of [EventTarget, Node, Element, CharacterData, Document, DocumentFragment, HTMLElement]) {
        markPrototypeNative(ctor);
    }
}
