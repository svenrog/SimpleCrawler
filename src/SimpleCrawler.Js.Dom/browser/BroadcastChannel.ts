import { enqueue } from "../scheduler/taskQueue";

// One render is one browsing context, so a channel's only peers are the other channels this page opened on
// the same name. A page uses one to keep tabs in step (a cart, a consent choice, an auth token) and does not
// guard the constructor; without it the module that opens the channel loses everything it went on to define.
const _channels: Record<string, BroadcastChannel[]> = {};

export class BroadcastChannel {
    readonly name: string;
    onmessage: ((ev: any) => void) | null = null;
    onmessageerror: ((ev: any) => void) | null = null;
    private closed = false;

    constructor(name: string) {
        this.name = String(name);
        (_channels[this.name] || (_channels[this.name] = [])).push(this);
    }

    postMessage(data: any): void {
        if (this.closed) return;
        for (const peer of _channels[this.name] || []) {
            if (peer === this || peer.closed) continue;
            enqueue(() => { if (peer.onmessage) peer.onmessage({ data, type: "message", target: peer }); });
        }
    }

    close(): void {
        this.closed = true;
        const peers = _channels[this.name];
        if (!peers) return;
        const at = peers.indexOf(this);
        if (at >= 0) peers.splice(at, 1);
    }

    addEventListener(type: string, cb: (ev: any) => void): void {
        if (type === "message") this.onmessage = cb;
        else if (type === "messageerror") this.onmessageerror = cb;
    }

    removeEventListener(type: string, cb: (ev: any) => void): void {
        if (type === "message" && this.onmessage === cb) this.onmessage = null;
        else if (type === "messageerror" && this.onmessageerror === cb) this.onmessageerror = null;
    }

    dispatchEvent(): boolean {
        return true;
    }
}
