import { Blob } from "./Blob";

// A File is a named Blob, and upload widgets construct one bare (`new File([data], name)`) to feed their own
// preview/validation path during init — the same unguarded construction Blob already earns its place on.
// Nothing is ever read from disk here; the bytes are whatever the page supplied.
export class File extends Blob {
    readonly name: string;
    readonly lastModified: number;
    readonly webkitRelativePath: string = "";

    constructor(parts?: any[], name?: unknown, options?: { type?: unknown; lastModified?: unknown }) {
        super(parts, options);
        this.name = name == null ? "" : String(name);
        this.lastModified = options && options.lastModified != null ? Number(options.lastModified) : Date.now();
    }
}
