import { Blob } from "./Blob";

// URL.createObjectURL hands the page a token standing for bytes the page already holds, so the token only
// has to be unique — nothing leaves the render to be fetched. But a page builds a script that way (a module
// shim rewriting imports, a worker body inlined by a bundler) and then loads the token, and the source it
// asks for is the blob it was handed: holding it here is what lets the host run that script rather than
// report a URL scheme it cannot fetch.
const _held = new Map<string, string>();

export function createObjectUrl(source: any): string {
    const url = "blob:" + Math.random().toString(36).slice(2);
    if (source && typeof source._text === "function") {
        try { _held.set(url, source._text()); } catch { /* a source whose bytes cannot be decoded is not a script */ }
    }
    return url;
}

export function revokeObjectUrl(url: any): void {
    _held.delete(String(url));
}

// The text a token stands for, or null for one this render did not build. A revoked token answers null,
// as a browser's fetch of one fails — the caller reads it when the node is connected, which is when a
// browser starts the fetch, so the common revoke-right-after-append is not a lost source.
export function objectUrlSource(url: string): string | null {
    const held = _held.get(url);
    return held === undefined ? null : held;
}
