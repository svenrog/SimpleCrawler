// Analytics and session-replay SDKs offload work to a worker while installing themselves, and they construct
// it *bare* — `new Worker(url)`, with no feature test — which is what justifies stubbing it where WebSocket
// and Notification are declined: absence here is not a branch the page chooses, it is a guaranteed
// ReferenceError inside the SDK's init, and an init that throws sets none of the globals it would have.
//
// Nothing runs inside this one: the worker's script is never fetched or executed, so it is a receiver that
// acknowledges nothing. It never fires message/error deliberately — an error invites a fallback or a retry,
// each re-arming a timer the drain must then pump, while silence merely leaves whatever the page expected
// back unresolved. Either way the SDK installs itself and sets its globals, which is the whole signal; what
// the worker would have computed is not observable from the window.
export class Worker {
    onmessage: ((...args: any[]) => void) | null = null;
    onmessageerror: ((...args: any[]) => void) | null = null;
    onerror: ((...args: any[]) => void) | null = null;

    constructor(_url?: unknown, _options?: unknown) { }

    postMessage(): void { }
    terminate(): void { }
    addEventListener(): void { }
    removeEventListener(): void { }
    dispatchEvent(): boolean { return false; }
}
