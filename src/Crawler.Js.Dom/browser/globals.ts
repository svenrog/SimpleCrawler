import { Document } from "../dom/Document";
import { Node } from "../dom/Node";
import { Element } from "../dom/Element";
import { Text } from "../dom/Text";
import { Comment } from "../dom/Comment";
import { DocumentFragment } from "../dom/DocumentFragment";
import { HTMLElement } from "../dom/HTMLElement";
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
import { documentRef } from "../dom/documentRef";

export const doc = new Document(globalThis as any);
documentRef.current = doc;

export function installDOM(global: any): void {
    global.document = doc;
    global.window = global;
    global.self = global;
    global.navigator = navigator;
    global.console = global.console || {
        log() { }, warn() { }, error() { }, info() { }, debug() { },
    };
    global.location = createLocation();
    global.history = createHistory();
    global.addEventListener = () => { };
    global.removeEventListener = () => { };
    global.dispatchEvent = () => true;
    global.matchMedia = () => ({
        matches: false,
        addListener() { }, removeListener() { },
        addEventListener() { }, removeEventListener() { },
    });
    global.getComputedStyle = () => ({ getPropertyValue: () => "" });
    global.MutationObserver = function () {
        this.observe = () => { };
        this.disconnect = () => { };
        this.takeRecords = () => [];
    };
    global.URL = URL;
    global.URLSearchParams = URLSearchParams;
    global.Node = Node;
    global.Element = Element;
    global.Document = Document;
    global.Text = Text;
    global.Comment = Comment;
    global.DocumentFragment = DocumentFragment;
    global.HTMLElement = HTMLElement;
    global.HTMLTemplateElement = HTMLTemplateElement;
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
}
