// What every getElementsBy* and .children answers with, in place of the plain array they returned before:
// `item` is how pre-querySelector code indexes one (a Magento inline script clears its cached menu classes
// with `navContainer.getElementsByTagName("li").item(i)`), and reading it off an array is a TypeError that
// costs the rest of that script. Extending Array keeps every internal array use — length, indexing,
// iteration, filter — working exactly as before, the way NodeList already does for querySelectorAll.
export class HTMLCollection<T = any> extends Array<T> {
    item(index: number): T | null {
        return this[index] ?? null;
    }

    // Browsers key this on id first, then on the name attribute for the elements that carry one.
    namedItem(name: string): T | null {
        const key = String(name);
        for (const node of this) {
            const el = node as any;
            if (!el || typeof el.getAttributeInternal !== "function") continue;
            if (el.getAttributeInternal("id") === key || el.getAttributeInternal("name") === key) return node;
        }
        return null;
    }
}
