import { DocumentFragment } from "./DocumentFragment";

// What attachShadow answers with. A fragment is what it behaves like; the distinct type exists because page
// code brands against it (`root instanceof ShadowRoot`) and the shadow-DOM polyfills patch the constructor's
// prototype by name before doing anything else.
export class ShadowRoot extends DocumentFragment {
    host: any = null;
    mode: string = "open";

    get nodeName(): string {
        return "#document-fragment";
    }
}
