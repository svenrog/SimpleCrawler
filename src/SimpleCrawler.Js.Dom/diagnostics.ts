// A shim-level exception that the task pump, event dispatch, or resource-event loop deliberately catches so
// one bad callback can't abort the whole drain. A real browser surfaces these to console/error handling; here
// they were swallowed in silence — which is exactly how a fatal hydration/commit throw presents as "the
// render just settled" with zero diagnostics. Route them to a dedicated host channel (embedded
// unconditionally, unlike the opt-in console bridge) at debug level, so raising the renderer's log level to
// Debug turns every silent settle into a named exception with a stack — without spamming a normal crawl.
export function reportSwallowed(context: string, error: unknown): void {
    try {
        const report = (globalThis as any).__crawlerDiagnostic;
        if (typeof report !== "function") return;
        const detail = error instanceof Error ? (error.stack || error.message || String(error)) : String(error);
        report("swallowed exception in " + context + ": " + detail);
    } catch { /* diagnostics must never itself abort the drain */ }
}
