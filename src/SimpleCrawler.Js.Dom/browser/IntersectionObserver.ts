import { enqueue } from "../scheduler/taskQueue";

// A real IntersectionObserver fires its callback asynchronously; the headless render has no scroll, so every
// observed element is reported as fully intersecting once. Lazy-mount-on-visible blocks (e.g. AntD skeletons
// that swap to real content when isIntersecting) would otherwise stay as placeholders forever.
export class IntersectionObserver {
    private _callback: (entries: any[], observer: IntersectionObserver) => void;
    private _pending: any[] = [];
    private _scheduled = false;

    constructor(callback: (entries: any[], observer: IntersectionObserver) => void) {
        this._callback = typeof callback === "function" ? callback : () => { };
    }

    observe(target: any): void {
        const rect = target && typeof target.getBoundingClientRect === "function"
            ? target.getBoundingClientRect()
            : { top: 0, left: 0, right: 0, bottom: 0, width: 0, height: 0, x: 0, y: 0 };
        this._pending.push({
            target,
            isIntersecting: true,
            intersectionRatio: 1,
            boundingClientRect: rect,
            intersectionRect: rect,
            rootBounds: rect,
            time: 0,
        });
        if (!this._scheduled) {
            this._scheduled = true;
            enqueue(() => this._flush());
        }
    }

    unobserve(): void { }
    disconnect(): void { this._pending = []; this._scheduled = false; }
    takeRecords(): any[] { return []; }

    private _flush(): void {
        this._scheduled = false;
        if (!this._pending.length) return;
        const entries = this._pending;
        this._pending = [];
        this._callback(entries, this);
    }
}
