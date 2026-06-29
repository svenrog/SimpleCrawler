type Callback = () => void;

const tasks: Callback[] = [];

export function enqueue(cb: unknown): number {
    if (typeof cb === "function") tasks.push(cb as Callback);
    return tasks.length;
}

export function pendingCount(): number {
    return tasks.length;
}

export function pumpTasks(): number {
    if (!tasks.length) return 0;
    const batch = tasks.splice(0, tasks.length);
    for (const fn of batch) {
        try { fn(); } catch { /* a failing callback must not abort the drain */ }
    }
    return tasks.length;
}

export function installTimerGlobals(global: any): void {
    global.queueMicrotask = (cb: Callback) => enqueue(cb);
    global.setTimeout = (cb: Callback) => enqueue(cb);
    global.clearTimeout = () => { };
    global.setInterval = () => 0;
    global.clearInterval = () => { };
    global.requestAnimationFrame = (cb: (t: number) => void) => enqueue(() => cb(0));
    global.cancelAnimationFrame = () => { };
}
