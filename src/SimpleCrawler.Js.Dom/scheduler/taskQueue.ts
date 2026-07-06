type Callback = () => void;

interface Task {
    id: number;
    cb: Callback;
    cancelled: boolean;
}

// A render collapses wall-clock time: every queued task runs back-to-back with no real delay. Timers
// longer than this are "give-up"/retry guards — webpack's 120s chunk-load timeout, reconnect backoffs,
// analytics flush deadlines — that a real browser always clears (the awaited load resolves in a few ms)
// long before they elapse. Running them here fires spuriously: a chunk the resource drain loaded still
// reports "Loading chunk failed (timeout)". So they are dropped, never counted. Short timers — React's
// scheduler, a code-split route's fallback delay — stay under the bar and run.
const _longTimerMs = 4000;

let _seq = 0;
const _tasks: Task[] = [];
const _byId = new Map<number, Task>();

export function enqueue(cb: unknown): number {
    if (typeof cb !== "function") return 0;
    const id = ++_seq;
    const task: Task = { id, cb: cb as Callback, cancelled: false };
    _tasks.push(task);
    _byId.set(id, task);
    return id;
}

export function cancel(id: unknown): void {
    if (typeof id !== "number") return;
    const task = _byId.get(id);
    if (task) {
        task.cancelled = true;
        _byId.delete(id);
    }
}

export function pendingCount(): number {
    return _tasks.length;
}

// Drops any tasks left queued from the previous page when the engine's realm is reused (Jint pool). A
// settled drain leaves this empty, but a page that hit the drain cap can leave stragglers; the id counter
// is kept monotonic so a stale id from the previous page can never alias a live one.
export function resetTasks(): void {
    _tasks.length = 0;
    _byId.clear();
}

export function pumpTasks(): number {
    if (!_tasks.length) return 0;
    const batch = _tasks.splice(0, _tasks.length);
    for (const task of batch) {
        _byId.delete(task.id);
        if (task.cancelled) continue;
        try { task.cb(); } catch { /* a failing callback must not abort the drain */ }
    }
    return _tasks.length;
}

export function installTimerGlobals(global: any): void {
    global.queueMicrotask = (cb: Callback) => enqueue(cb);
    global.setTimeout = (cb: Callback, delay?: number) =>
        typeof delay === "number" && delay > _longTimerMs ? ++_seq : enqueue(cb);
    global.clearTimeout = (id: unknown) => cancel(id);
    global.setInterval = () => 0;
    global.clearInterval = () => { };
    global.requestAnimationFrame = (cb: (t: number) => void) => enqueue(() => cb(0));
    global.cancelAnimationFrame = (id: unknown) => cancel(id);
}
