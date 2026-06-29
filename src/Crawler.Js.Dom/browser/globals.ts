import { Document } from "../dom/Document";
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
    installTimerGlobals(global);
}
