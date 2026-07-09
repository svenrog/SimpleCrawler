// File inputs never carry real files in a headless render, so an empty array-like list is all a bundle
// needs: it satisfies `instanceof FileList` and any length/iteration probe without crashing.
export class FileList {
    readonly length: number = 0;

    item(_index: number): null {
        return null;
    }

    [Symbol.iterator](): Iterator<any> {
        return { next() { return { value: undefined, done: true }; } };
    }
}
