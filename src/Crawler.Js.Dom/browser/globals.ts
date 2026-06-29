import { Document } from "../dom/Document";
import { Node } from "../dom/Node";
import { Element } from "../dom/Element";
import { Text } from "../dom/Text";
import { Comment } from "../dom/Comment";
import { DocumentFragment } from "../dom/DocumentFragment";
import { HTMLElement } from "../dom/HTMLElement";
import { customElements } from "../dom/customElements";
import { navigator } from "./navigator";
import { createLocation } from "./location";
import { createHistory } from "./history";
import { installTimerGlobals } from "../scheduler/taskQueue";
import { URL } from "../url/Url";
import { URLSearchParams } from "../url/URLSearchParams";

export const doc = new Document(globalThis as any);

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
    global.customElements = customElements;
    customElements.setDocument(doc);
    installTimerGlobals(global);
}
