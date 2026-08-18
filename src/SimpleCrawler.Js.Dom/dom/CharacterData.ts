import { Node } from "./Node";
import { NodeType } from "../types/NodeType";

export abstract class CharacterData extends Node {
    data: string;

    protected constructor(type: NodeType, data: unknown) {
        super(type);
        this.data = data == null ? "" : String(data);
    }

    get nodeValue(): string {
        return this.data;
    }
    set nodeValue(v: unknown) {
        this.data = v == null ? "" : String(v);
    }

    // Text carried this alone, which left a comment's textContent undefined — and hydration finds its
    // boundaries by walking childNodes for `8 === n.nodeType && n.textContent.trim() === marker`.
    get textContent(): string {
        return this.data;
    }
    set textContent(v: unknown) {
        this.data = v == null ? "" : String(v);
    }

    get length(): number {
        return this.data.length;
    }

    appendData(v: unknown): void {
        this.data += v == null ? "" : String(v);
    }

    substringData(offset: number, count: number): string {
        const start = Math.max(0, Number(offset) || 0);
        return this.data.slice(start, start + Math.max(0, Number(count) || 0));
    }
}
