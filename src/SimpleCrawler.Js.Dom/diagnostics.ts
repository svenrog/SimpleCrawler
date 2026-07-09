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
        report("swallowed exception in " + context + ": " + describeError(error));
    } catch { /* diagnostics must never itself abort the drain */ }
}

// V8 begins `error.stack` with the message line, but Jint's stack is frames only — so reporting the stack
// alone drops the message (e.g. React's "Minified React error #147", the one datum that identifies the
// failure). Emit the message first, then the stack only when it doesn't already repeat it.
function describeError(error: unknown): string {
    if (!(error instanceof Error)) return String(error);
    const message = error.message || String(error);
    const stack = error.stack;
    if (!stack) return message;
    return stack.indexOf(message) >= 0 ? stack : message + "\n" + stack;
}
