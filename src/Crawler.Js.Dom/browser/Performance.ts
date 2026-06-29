const startTime = Date.now();

export class Performance {
    readonly timeOrigin = startTime;

    now(): number {
        return Date.now() - startTime;
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
