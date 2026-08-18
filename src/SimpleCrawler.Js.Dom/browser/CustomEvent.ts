import { Event } from "./Event";

export class CustomEvent extends Event {
    detail: any;

    constructor(type: string, init?: any) {
        super(type, init);
        this.detail = init && init.detail !== undefined ? init.detail : null;
    }

    initCustomEvent(type: unknown, bubbles?: unknown, cancelable?: unknown, detail?: unknown): void {
        this.initEvent(type, bubbles, cancelable);
        this.detail = detail === undefined ? null : detail;
    }
}
