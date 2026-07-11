export const _zeroRect = Object.freeze({ top: 0, left: 0, right: 0, bottom: 0, width: 0, height: 0, x: 0, y: 0 });

// Lazy-loaders feature-detect IntersectionObserver support with `'isIntersecting' in
// IntersectionObserverEntry.prototype` (instant-page does exactly this), so the global and its prototype
// fields must exist or the probe throws a ReferenceError before the page hydrates. The observer reports
// plain entry objects (see IntersectionObserver) rather than instances, so this is never constructed — it
// exists purely so the detection evaluates instead of throwing.
export class IntersectionObserverEntry {
    get boundingClientRect(): any { return _zeroRect; }
    get intersectionRect(): any { return _zeroRect; }
    get rootBounds(): any { return null; }
    get intersectionRatio(): number { return 0; }
    get isIntersecting(): boolean { return false; }
    get target(): any { return null; }
    get time(): number { return 0; }
}
