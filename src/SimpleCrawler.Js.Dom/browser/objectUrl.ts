import { Blob } from "./Blob";

// URL.createObjectURL hands the page a token for bytes it already holds. A page builds a script that way —
// a module shim rewriting imports, a worker body inlined by a bundler — and then loads the token, so keeping
// what a token stands for is what lets the host run that script instead of reporting a scheme it cannot
// fetch. The blob itself is kept, never its text: the page's own reference already keeps those bytes alive
// until it revokes, and decoding one the page never loads as a script would cost a copy of every image,
// download and video a page builds a URL for.
const _held = new Map<string, Blob>();

// The types a browser will execute a blob script for. An empty type is not one of them there, and is here:
// a page that builds its shim without a type is asking for the source it just wrote.
const _scriptTypes = ["", "text/javascript", "application/javascript", "text/ecmascript", "application/ecmascript", "module"];

export function createObjectUrl(source: any): string {
    const url = "blob:" + Math.random().toString(36).slice(2);
    if (source instanceof Blob) _held.set(url, source);
    return url;
}

export function revokeObjectUrl(url: any): void {
    _held.delete(String(url));
}

// The source a token stands for, or null for one this render did not build, already revoked, or holding
// something no browser would have executed. The caller reads it when the node is connected, which is when a
// browser starts the fetch — so the usual revoke on the line after appendChild is not a lost source.
export function objectUrlSource(url: string): string | null {
    const blob = _held.get(url);
    if (!blob) return null;
    const type = String(blob.type || "").split(";")[0].trim().toLowerCase();
    return _scriptTypes.indexOf(type) === -1 ? null : blob._text();
}
