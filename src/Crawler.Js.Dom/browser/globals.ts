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
import { TextEncoder } from "./TextEncoder";
import { TextDecoder } from "./TextDecoder";
import { crypto } from "./crypto";
import { MessageChannel, MessagePort } from "./MessageChannel";
import { createStorage } from "./Storage";
import { performance } from "./Performance";
import { installViewport } from "./viewport";
import { IntersectionObserver } from "./IntersectionObserver";
import { Blob } from "./Blob";
import { btoa, atob } from "./base64";
import { documentRef } from "../dom/documentRef";
import { installScrollApi } from "./scroll";

export const doc = new Document(globalThis as any);
documentRef.current = doc;

export function installDOM(global: any): void {
    global.document = doc;
    global.window = global;
    global.self = global;
    global.navigator = navigator;
    global.location = createLocation();
    global.history = createHistory();
    global.addEventListener = () => { };
    global.removeEventListener = () => { };
    global.dispatchEvent = () => true;
    // Handler props declared null so `'onX' in window` feature-detection passes and bundle assignments stick;
    // events never fire in the single-pass render, so the assigned handlers are only ever stored.
    for (const on of ["onresize", "onscroll", "onload", "onerror", "onunload", "onbeforeunload",
        "onpopstate", "onhashchange", "onpageshow", "onpagehide", "onmessage", "onoffline", "ononline",
        "onfocus", "onblur", "onorientationchange"]) {
        if (!(on in global)) global[on] = null;
    }
    global.getComputedStyle = () => ({ getPropertyValue: () => "" });
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
    global.ResizeObserver = function () {
        this.observe = () => { };
        this.unobserve = () => { };
        this.disconnect = () => { };
    };
    // A deep-clone good enough for the JSON-serialisable state bundles round-trip; non-JSON inputs
    // (functions, cycles) aren't supported, matching nothing real but never reached by our targets.
    global.structuredClone = global.structuredClone || ((value: any) => value == null ? value : JSON.parse(JSON.stringify(value)));
    global.Blob = Blob;
    global.btoa = global.btoa || btoa;
    global.atob = global.atob || atob;
    // Blobs never leave the render, so an object URL only needs to be a unique, revocable token.
    (URL as any).createObjectURL = (URL as any).createObjectURL || (() => "blob:" + Math.random().toString(36).slice(2));
    (URL as any).revokeObjectURL = (URL as any).revokeObjectURL || (() => { });
    global.URL = URL;
    global.URLSearchParams = URLSearchParams;
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
    global.Image = HTMLImageElement;
    for (const name in htmlInterfaces) global[name] = (htmlInterfaces as any)[name];
    global.customElements = customElements;
    customElements.setDocument(doc);
    global.Event = Event;
    global.CustomEvent = CustomEvent;
    global.TextEncoder = global.TextEncoder || TextEncoder;
    global.TextDecoder = global.TextDecoder || TextDecoder;
    global.crypto = global.crypto || crypto;
    global.MessageChannel = global.MessageChannel || MessageChannel;
    global.MessagePort = global.MessagePort || MessagePort;
    global.performance = global.performance || performance;
    global.localStorage = createStorage();
    global.sessionStorage = createStorage();
    installTimerGlobals(global);
    installScrollApi(global);
}
