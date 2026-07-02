// IndexedDB ships as a separate opt-in prelude (JsRenderOptions.EnableIndexedDb), so it can't import the
// scheduler module directly — a second esbuild bundle would get its own task queue that the host never
// drains. Instead it reuses dom.js's queue through the already-installed global queueMicrotask (aliased to
// the same enqueue), captured once at load, after installTimerGlobals has run and before any bundle script.
const queue: (cb: () => void) => void = (globalThis as any).queueMicrotask;

export function enqueue(cb: () => void): void {
    queue(cb);
}
