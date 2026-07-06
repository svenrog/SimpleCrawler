// Backs querySelectorAll and is exposed as the `NodeList` global so bundles that branch on
// `x instanceof NodeList` (slider/drag libraries normalising their element inputs) match instead of
// throwing "NodeList is not defined". Extending Array keeps every internal array use working.
export class NodeList<T = any> extends Array<T> {
    item(index: number): T | null {
        return this[index] ?? null;
    }
}
