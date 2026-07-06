// When the host exposes a native high-resolution clock (ClearScript's capital-P `Performance`, added only
// under profiling), borrow it so now() is sub-millisecond; otherwise fall back to whole-millisecond Date.now.
const _hostPerf: any = (globalThis as any).Performance;
const _hostNow: (() => number) | null =
    _hostPerf && typeof _hostPerf.now === "function" ? () => _hostPerf.now() : null;

const startTime = _hostNow ? _hostNow() : Date.now();

export class Performance {
    readonly timeOrigin = startTime;

    now(): number {
        return _hostNow ? _hostNow() - startTime : Date.now() - startTime;
    }

    mark(): any { return null; }
    measure(): any { return null; }
    clearMarks(): void { }
    clearMeasures(): void { }
    getEntries(): any[] { return []; }
    getEntriesByName(): any[] { return []; }
    getEntriesByType(): any[] { return []; }
}

export const performance = new Performance();
