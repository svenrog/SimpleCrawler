import { createContext2D } from "./HTMLCanvasElement";

// Graphics widgets composite off-screen with `new OffscreenCanvas(w, h)` then `.getContext("2d")` and draw
// calls, sometimes inside an effect during hydration; a missing global is a ReferenceError that fails the
// subtree. Layout-less, so the context is the same no-op 2d stub as <canvas> and the produced bitmap/blob
// are inert.
export class OffscreenCanvas {
    width: number;
    height: number;

    constructor(width?: number, height?: number) {
        this.width = width || 0;
        this.height = height || 0;
    }

    getContext(type: string): any {
        return type === "2d" ? createContext2D(this) : null;
    }

    transferToImageBitmap(): any {
        return { width: this.width, height: this.height, close() { } };
    }

    convertToBlob(): Promise<any> {
        return Promise.resolve(null);
    }

    close(): void { }
}
