// When the host exposes a native high-resolution clock (ClearScript's capital-P `Performance`, added only
// under profiling), borrow it so now() is sub-millisecond; otherwise fall back to whole-millisecond Date.now.
const _hostPerf: any = (globalThis as any).Performance;
const _hostNow: (() => number) | null =
    _hostPerf && typeof _hostPerf.now === "function" ? () => _hostPerf.now() : null;

const startTime = _hostNow ? _hostNow() : Date.now();

// The legacy Navigation Timing Level 1 surface. Every field is a Unix-epoch millisecond, and the render has
// no navigation to measure: the document was already fetched by the host and is parsed in one pass, so every
// phase reports the same instant. Callers subtract these from each other (`domInteractive - navigationStart`)
// and divide by them — an absent `timing` is a TypeError during a metrics SDK's init, a zero-length phase is
// merely an uninteresting measurement.
const _epochStart = Date.now();

export class PerformanceTiming {
    readonly navigationStart = _epochStart;
    readonly unloadEventStart = 0;
    readonly unloadEventEnd = 0;
    readonly redirectStart = 0;
    readonly redirectEnd = 0;
    readonly fetchStart = _epochStart;
    readonly domainLookupStart = _epochStart;
    readonly domainLookupEnd = _epochStart;
    readonly connectStart = _epochStart;
    readonly connectEnd = _epochStart;
    readonly secureConnectionStart = 0;
    readonly requestStart = _epochStart;
    readonly responseStart = _epochStart;
    readonly responseEnd = _epochStart;
    readonly domLoading = _epochStart;
    readonly domInteractive = _epochStart;
    readonly domContentLoadedEventStart = _epochStart;
    readonly domContentLoadedEventEnd = _epochStart;
    readonly domComplete = _epochStart;
    readonly loadEventStart = _epochStart;
    readonly loadEventEnd = _epochStart;
}

// The Level 2 replacement for the same measurement, reached through getEntriesByType("navigation")[0].
// Times here are relative to timeOrigin, so they are all zero for the same reason the timing fields are all
// equal. `type: "navigate"` is what a first load reports; nothing in a single-pass render is a reload or a
// back-forward restore.
export class PerformanceNavigationTiming {
    readonly entryType = "navigation";
    // Assigned when the entry is handed out, not here: this instance is built while the prelude loads, and
    // the page URL only arrives afterwards (__crawlerSetLocation).
    name = "";
    readonly initiatorType = "navigation";
    readonly type = "navigate";
    readonly startTime = 0;
    readonly duration = 0;
    readonly fetchStart = 0;
    readonly domainLookupStart = 0;
    readonly domainLookupEnd = 0;
    readonly connectStart = 0;
    readonly connectEnd = 0;
    readonly secureConnectionStart = 0;
    readonly requestStart = 0;
    readonly responseStart = 0;
    readonly responseEnd = 0;
    readonly domInteractive = 0;
    readonly domContentLoadedEventStart = 0;
    readonly domContentLoadedEventEnd = 0;
    readonly domComplete = 0;
    readonly loadEventStart = 0;
    readonly loadEventEnd = 0;
    readonly redirectCount = 0;
    readonly transferSize = 0;
    readonly encodedBodySize = 0;
    readonly decodedBodySize = 0;

    toJSON(): any {
        return { ...this };
    }
}

export class Performance {
    readonly timeOrigin = startTime;
    readonly timing = new PerformanceTiming();
    // The Level 1 navigation type: 0 is TYPE_NAVIGATE, and no redirect was followed inside the render.
    readonly navigation = { type: 0, redirectCount: 0 };

    private readonly _navigationEntry = new PerformanceNavigationTiming();

    now(): number {
        return _hostNow ? _hostNow() - startTime : Date.now() - startTime;
    }

    mark(): any { return null; }
    measure(): any { return null; }
    clearMarks(): void { }
    clearMeasures(): void { }

    // Only the navigation entry exists: no resource, paint or long-task entry is observable in a render that
    // fetches through the host and never paints. Every other type answers with the empty list a browser gives
    // before anything of that type has happened.
    getEntries(): any[] { return [this._entry()]; }

    getEntriesByName(name: unknown): any[] {
        const entry = this._entry();
        return entry.name === String(name) ? [entry] : [];
    }

    getEntriesByType(type: unknown): any[] {
        return String(type) === "navigation" ? [this._entry()] : [];
    }

    private _entry(): PerformanceNavigationTiming {
        this._navigationEntry.name = String((globalThis as any).location?.href || "");
        return this._navigationEntry;
    }
}

export const performance = new Performance();
