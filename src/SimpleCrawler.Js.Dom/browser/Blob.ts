import { TextEncoder } from "./TextEncoder";
import { TextDecoder } from "./TextDecoder";

const _encoder = new TextEncoder();
const _decoder = new TextDecoder();

function partBytes(part: any): Uint8Array {
    if (part instanceof Blob) return part._bytes();
    if (part instanceof Uint8Array) return part;
    if (part instanceof ArrayBuffer) return new Uint8Array(part);
    if (part && ArrayBuffer.isView(part)) return new Uint8Array(part.buffer, part.byteOffset, part.byteLength);
    return _encoder.encode(part == null ? "" : String(part));
}

export class Blob {
    readonly type: string;
    private readonly _parts: Uint8Array[];

    constructor(parts?: any[], options?: { type?: unknown }) {
        this._parts = (parts || []).map(partBytes);
        this.type = options && options.type != null ? String(options.type).toLowerCase() : "";
    }

    get size(): number {
        let n = 0;
        for (const p of this._parts) n += p.length;
        return n;
    }

    _bytes(): Uint8Array {
        const out = new Uint8Array(this.size);
        let at = 0;
        for (const p of this._parts) { out.set(p, at); at += p.length; }
        return out;
    }

    arrayBuffer(): Promise<ArrayBuffer> {
        return Promise.resolve(this._bytes().buffer as ArrayBuffer);
    }

    text(): Promise<string> {
        return Promise.resolve(this._text());
    }

    _text(): string {
        return _decoder.decode(this._bytes());
    }

    slice(start?: number, end?: number, contentType?: string): Blob {
        const bytes = this._bytes();
        const b = new Blob([bytes.slice(start, end)]);
        (b as any).type = contentType == null ? "" : String(contentType).toLowerCase();
        return b;
    }
}
