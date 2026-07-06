import { enqueue } from "../scheduler/taskQueue";

export class MessagePort {
    onmessage: ((ev: any) => void) | null = null;
    private other: MessagePort | null = null;

    postMessage(data: any): void {
        const o = this.other;
        if (o) enqueue(() => { if (o.onmessage) o.onmessage({ data }); });
    }

    start(): void { }

    close(): void { }

    addEventListener(type: string, cb: (ev: any) => void): void {
        if (type === "message") this.onmessage = cb;
    }

    removeEventListener(type: string, cb: (ev: any) => void): void {
        if (type === "message" && this.onmessage === cb) this.onmessage = null;
    }

    _link(other: MessagePort): void {
        this.other = other;
    }
}

export class MessageChannel {
    readonly port1: MessagePort;
    readonly port2: MessagePort;

    constructor() {
        this.port1 = new MessagePort();
        this.port2 = new MessagePort();
        this.port1._link(this.port2);
        this.port2._link(this.port1);
    }
}
