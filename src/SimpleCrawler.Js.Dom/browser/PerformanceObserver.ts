// Web-vitals/RUM/tracing code constructs one of these while the SDK initializes — Sentry's browser tracing
// and Vercel's Speed Insights among them — so a missing global threw a ReferenceError straight through the
// SDK's init and cost every global it (and anything sharing its entry) would have set.
//
// The callback never fires, and that is faithful rather than lazy: a PerformanceObserver reports timing
// entries, and the layout-less single-pass render performs no paint, no layout and no navigation, so no entry
// ever occurs — the same reason `performance.getEntries()` already answers []. This is the inverse of
// IntersectionObserver, which does fire because "is this element visible" has a defensible answer here (yes);
// "when did the largest contentful paint happen" does not.
//
// supportedEntryTypes is populated rather than empty because the honest state is "a browser that supports
// these, with nothing measured yet" — which is exactly what a real one reports before the first entry lands.
// An empty list instead reads as "this browser cannot do web vitals", pushing every caller down its
// unsupported-browser branch; both are safe, but this one matches the rest of the DOM's pretense and is what
// was measured. Nothing awaits a callback that never comes, so a caller simply never reports its metric.
//
// This is the one shim in this batch with a measured recovery rather than an argued one: supplying it alone
// takes a Next.js marketing page from four globals to six, because the SDK constructing it here also carried
// a syntax highlighter into the same entry — one ReferenceError, two technologies reported absent.
const _supportedEntryTypes = [
    "element",
    "event",
    "first-input",
    "largest-contentful-paint",
    "layout-shift",
    "longtask",
    "mark",
    "measure",
    "navigation",
    "paint",
    "resource",
    "visibility-state",
];

export class PerformanceObserver {
    static readonly supportedEntryTypes = _supportedEntryTypes;

    constructor(_callback?: (list: any, observer: PerformanceObserver) => void) { }

    observe(): void { }
    disconnect(): void { }
    takeRecords(): any[] { return []; }
}
