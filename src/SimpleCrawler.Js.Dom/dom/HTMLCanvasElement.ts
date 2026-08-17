import { HTMLElement } from "./HTMLElement";
import { createWebGLContext, isWebGlContextType, isWebGlEnabled } from "./webgl";

// A layout-less canvas can't paint, but animation libraries (lottie, confetti, chart widgets) mount by
// grabbing a context synchronously — `canvas.getContext("2d")` then calling draw methods on the result — so
// the method and a no-op context must exist or the mount throws and trips the SPA error boundary. Every
// drawing call is swallowed; the few accessors that must hand back an object (measureText, gradients,
// image data) return zero-sized stubs so chained reads don't fault.
export function createContext2D(canvas: any): any {
    const noop = () => { };
    return {
        canvas,
        fillStyle: "#000000",
        strokeStyle: "#000000",
        globalAlpha: 1,
        globalCompositeOperation: "source-over",
        lineWidth: 1,
        lineCap: "butt",
        lineJoin: "miter",
        font: "10px sans-serif",
        textAlign: "start",
        textBaseline: "alphabetic",
        save: noop, restore: noop,
        scale: noop, rotate: noop, translate: noop, transform: noop, setTransform: noop, resetTransform: noop,
        beginPath: noop, closePath: noop, moveTo: noop, lineTo: noop,
        bezierCurveTo: noop, quadraticCurveTo: noop, arc: noop, arcTo: noop, ellipse: noop, rect: noop,
        fill: noop, stroke: noop, clip: noop,
        fillRect: noop, strokeRect: noop, clearRect: noop,
        fillText: noop, strokeText: noop,
        drawImage: noop, putImageData: noop,
        setLineDash: noop, getLineDash: () => [],
        measureText: () => ({ width: 0, actualBoundingBoxAscent: 0, actualBoundingBoxDescent: 0 }),
        createLinearGradient: () => ({ addColorStop: noop }),
        createRadialGradient: () => ({ addColorStop: noop }),
        createPattern: () => null,
        createImageData: (w: number, h: number) => ({ width: w || 0, height: h || 0, data: new Uint8ClampedArray(Math.max(0, (w || 0) * (h || 0) * 4)) }),
        getImageData: (_x: number, _y: number, w: number, h: number) => ({ width: w || 0, height: h || 0, data: new Uint8ClampedArray(Math.max(0, (w || 0) * (h || 0) * 4)) }),
    };
}

export class HTMLCanvasElement extends HTMLElement {
    constructor() {
        super("canvas");
    }

    get width(): number {
        const v = parseInt(this.getAttributeInternal("width") || "", 10);
        return isNaN(v) ? 300 : v;
    }

    set width(value: unknown) {
        this.setAttributeInternal("width", String(value == null ? 0 : value));
    }

    get height(): number {
        const v = parseInt(this.getAttributeInternal("height") || "", 10);
        return isNaN(v) ? 150 : v;
    }

    set height(value: unknown) {
        this.setAttributeInternal("height", String(value == null ? 0 : value));
    }

    getContext(type: string, attributes?: any): any {
        if (type === "2d") return createContext2D(this);
        // WebGL is opt-in (EnableWebGl): off, getContext returns null exactly as before, so a map/3D library
        // that probes for WebGL takes its unsupported path instead of a stub that would start fetching tiles.
        if (isWebGlContextType(type) && isWebGlEnabled()) return createWebGLContext(this, type, attributes);
        return null;
    }

    toDataURL(): string {
        return "data:,";
    }

    toBlob(callback: (blob: any) => void): void {
        if (typeof callback === "function") callback(null);
    }
}
